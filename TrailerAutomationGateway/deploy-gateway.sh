#!/bin/bash
# deploy-gateway.sh - Deploy TrailerAutomation Gateway updates

echo "Stopping service..."
sudo systemctl stop trailerautomation-gateway

echo "Setting permissions for file transfer..."
sudo chown -R pi:trailerautomation /home/pi/TrailerAutomationGateway
sudo chmod -R u+rwX /home/pi/TrailerAutomationGateway

echo "Ready for file transfer. Copy files now, then press Enter..."
read

echo "Fixing permissions..."
# Create data directory if it doesn't exist
mkdir -p /home/pi/TrailerAutomationGateway/data

# Set ownership - pi:trailerautomation
sudo chown -R pi:trailerautomation /home/pi/TrailerAutomationGateway

# Set permissions - owner and group can read/write
sudo find /home/pi/TrailerAutomationGateway -type d -exec chmod 770 {} \;
sudo find /home/pi/TrailerAutomationGateway -type f -exec chmod 660 {} \;

# Make executable executable for owner and group
sudo chmod 770 /home/pi/TrailerAutomationGateway/TrailerAutomationGateway

# Delete old database files (they may have wrong permissions)
rm -f /home/pi/TrailerAutomationGateway/data/*.db*

echo "Starting service..."
sudo systemctl start trailerautomation-gateway
sleep 2
sudo systemctl status trailerautomation-gateway

echo ""
echo "Deployment complete!"
echo "Watching logs (Ctrl+C to exit)..."
sudo journalctl -u trailerautomation-gateway -f
