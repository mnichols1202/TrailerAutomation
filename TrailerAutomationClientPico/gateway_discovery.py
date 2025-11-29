"""
mDNS Gateway Discovery for MicroPython
Discovers the TrailerAutomationGateway service on the local network.
"""

import socket
import time
import struct
import network

class MDNSQuery:
    """Simple mDNS query handler for service discovery."""
    
    MDNS_ADDR = "224.0.0.251"
    MDNS_PORT = 5353
    
    # DNS Record Types
    TYPE_A = 0x0001
    TYPE_PTR = 0x000C
    TYPE_TXT = 0x0010
    TYPE_SRV = 0x0021
    TYPE_AAAA = 0x001C
    
    # DNS Classes
    CLASS_IN = 0x0001
    
    def __init__(self, service_type):
        """
        Initialize mDNS query.
        
        Args:
            service_type: Service type to query (e.g., "_trailer-gateway._tcp.local")
        """
        self.service_type = service_type
        self.sock = None
    
    def _encode_name(self, name):
        """Encode a DNS name with length-prefixed labels."""
        encoded = bytearray()
        for label in name.split('.'):
            if label:
                encoded.append(len(label))
                encoded.extend(label.encode('utf-8'))
        encoded.append(0)  # Null terminator
        return bytes(encoded)
    
    def _decode_name(self, data, offset):
        """
        Decode a DNS name from the packet.
        
        Returns:
            tuple: (name_string, new_offset)
        """
        labels = []
        jumped = False
        orig_offset = offset
        max_offset = offset
        
        while True:
            if offset >= len(data):
                break
            
            length = data[offset]
            
            # Check for pointer (compression)
            if (length & 0xC0) == 0xC0:
                if not jumped:
                    max_offset = offset + 2
                if offset + 1 >= len(data):
                    break
                pointer = ((length & 0x3F) << 8) | data[offset + 1]
                offset = pointer
                jumped = True
                continue
            
            # End of name
            if length == 0:
                offset += 1
                break
            
            # Read label
            offset += 1
            if offset + length > len(data):
                break
            label = data[offset:offset + length].decode('utf-8', errors='ignore')
            labels.append(label)
            offset += length
        
        if jumped:
            return '.'.join(labels), max_offset
        else:
            return '.'.join(labels), offset
    
    def _create_query(self, query_name, query_type=TYPE_PTR):
        """Create an mDNS query packet."""
        # Transaction ID (0x0000 for mDNS)
        tid = 0x0000
        
        # Flags (standard query)
        flags = 0x0000
        
        # Question count
        qdcount = 1
        
        # Answer, Authority, Additional counts
        ancount = 0
        nscount = 0
        arcount = 0
        
        # Build header
        header = struct.pack('!HHHHHH', tid, flags, qdcount, ancount, nscount, arcount)
        
        # Build question
        qname = self._encode_name(query_name)
        qtype = struct.pack('!H', query_type)
        qclass = struct.pack('!H', self.CLASS_IN)
        
        return header + qname + qtype + qclass
    
    def _parse_response(self, data, timeout_time):
        """
        Parse mDNS response and extract relevant information.
        
        Returns:
            dict: Parsed response with 'srv', 'a', 'txt' records
        """
        if len(data) < 12:
            return None
        
        # Parse header
        tid, flags, qdcount, ancount, nscount, arcount = struct.unpack('!HHHHHH', data[0:12])
        
        offset = 12
        
        # Skip questions
        for _ in range(qdcount):
            if offset >= len(data):
                return None
            name, offset = self._decode_name(data, offset)
            offset += 4  # Skip QTYPE and QCLASS
        
        result = {'srv': None, 'a': [], 'txt': []}
        
        # Parse answers and additional records
        total_records = ancount + nscount + arcount
        
        for _ in range(total_records):
            if offset >= len(data):
                break
            
            # Parse resource record
            name, offset = self._decode_name(data, offset)
            
            if offset + 10 > len(data):
                break
            
            rtype, rclass, ttl, rdlength = struct.unpack('!HHIH', data[offset:offset + 10])
            offset += 10
            
            if offset + rdlength > len(data):
                break
            
            rdata = data[offset:offset + rdlength]
            offset += rdlength
            
            # Process SRV records
            if rtype == self.TYPE_SRV and rdlength >= 6:
                priority, weight, port = struct.unpack('!HHH', rdata[0:6])
                target, _ = self._decode_name(data, offset - rdlength + 6)
                
                if self.service_type.split('.')[0].replace('_', '') in name.lower():
                    result['srv'] = {
                        'priority': priority,
                        'weight': weight,
                        'port': port,
                        'target': target
                    }
            
            # Process A records (IPv4)
            elif rtype == self.TYPE_A and rdlength == 4:
                ip = '.'.join(str(b) for b in rdata)
                result['a'].append({'name': name, 'ip': ip})
        
        return result if (result['srv'] or result['a']) else None
    
    def query(self, timeout_sec=5):
        """
        Perform mDNS query for the service.
        
        Args:
            timeout_sec: Query timeout in seconds
        
        Returns:
            tuple: (ip_address, port) or (None, None) if not found
        """
        try:
            # Create UDP socket
            self.sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
            self.sock.settimeout(0.5)  # Short timeout for recv
            
            # Allow address reuse
            self.sock.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
            
            # Bind to any address
            self.sock.bind(('', self.MDNS_PORT))
            
            # Join multicast group
            mreq = struct.pack('4s4s', socket.inet_aton(self.MDNS_ADDR), socket.inet_aton('0.0.0.0'))
            self.sock.setsockopt(socket.IPPROTO_IP, socket.IP_ADD_MEMBERSHIP, mreq)
            
            # Create and send query
            query_packet = self._create_query(self.service_type, self.TYPE_PTR)
            self.sock.sendto(query_packet, (self.MDNS_ADDR, self.MDNS_PORT))
            
            print(f"[mDNS] Sent query for {self.service_type}")
            
            start_time = time.time()
            timeout_time = start_time + timeout_sec
            
            srv_record = None
            ip_candidates = []
            
            # Listen for responses
            while time.time() < timeout_time:
                try:
                    data, addr = self.sock.recvfrom(1024)
                    
                    # Parse response
                    parsed = self._parse_response(data, timeout_time)
                    
                    if parsed:
                        if parsed['srv']:
                            srv_record = parsed['srv']
                            print(f"[mDNS] Found SRV: target={srv_record['target']}, port={srv_record['port']}")
                        
                        if parsed['a']:
                            for a_record in parsed['a']:
                                ip_candidates.append(a_record)
                                print(f"[mDNS] Found A: {a_record['name']} -> {a_record['ip']}")
                        
                        # If we have both SRV and matching A record, we're done
                        if srv_record:
                            for a_rec in ip_candidates:
                                if srv_record['target'].lower() in a_rec['name'].lower() or \
                                   a_rec['name'].lower() in srv_record['target'].lower():
                                    return a_rec['ip'], srv_record['port']
                
                except OSError:
                    # Timeout on recv, continue listening
                    pass
            
            # If we got a response but couldn't match, try using local subnet heuristic
            if srv_record and ip_candidates:
                wlan = network.WLAN(network.STA_IF)
                if wlan.isconnected():
                    local_ip = wlan.ifconfig()[0]
                    local_subnet = '.'.join(local_ip.split('.')[:3])
                    
                    # Prefer IP from same subnet
                    for a_rec in ip_candidates:
                        if a_rec['ip'].startswith(local_subnet):
                            print(f"[mDNS] Using same-subnet IP: {a_rec['ip']}")
                            return a_rec['ip'], srv_record['port']
                    
                    # Fallback to first candidate
                    print(f"[mDNS] Using first candidate IP: {ip_candidates[0]['ip']}")
                    return ip_candidates[0]['ip'], srv_record['port']
            
            return None, None
            
        except Exception as e:
            print(f"[mDNS] Query error: {e}")
            return None, None
        
        finally:
            if self.sock:
                try:
                    self.sock.close()
                except:
                    pass

def discover_gateway(service_type="_trailer-gateway._tcp.local", timeout_sec=8):
    """
    Discover the TrailerAutomationGateway via mDNS.
    
    Args:
        service_type: mDNS service type to search for
        timeout_sec: Discovery timeout in seconds
    
    Returns:
        str: Gateway URL (e.g., "http://192.168.1.100:5000") or None if not found
    """
    print(f"[mDNS] Discovering {service_type}...")
    
    mdns = MDNSQuery(service_type)
    ip, port = mdns.query(timeout_sec=timeout_sec)
    
    if ip and port:
        gateway_url = f"http://{ip}:{port}"
        print(f"[mDNS] Gateway discovered: {gateway_url}")
        return gateway_url
    else:
        print(f"[mDNS] No gateway found after {timeout_sec}s")
        return None
