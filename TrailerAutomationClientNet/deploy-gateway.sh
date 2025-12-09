#!/bin/bash
# deploy-gateway.sh - Deploy TrailerAutomation Gateway updates

echo "Stopping service..."
sudo systemctl stop  trailer-automation-client

echo "Setting permissions for file transfer..."
sudo chown -R pi:trailerautomation /home/pi/TrailerAutomationNet
sudo chmod -R u+rwX /home/pi/TrailerAutomationNet

echo "Fixing permissions..."

# Set ownership - pi:trailerautomation
sudo chown -R pi:trailerautomation /home/pi/TrailerAutomationGateway

# Set permissions - owner and group can read/write
sudo find /home/pi/TrailerAutomationNet -type d -exec chmod 770 {} \;
sudo find /home/pi/TrailerAutomationNet -type f -exec chmod 660 {} \;

# Make executable executable for owner and group
sudo chmod 770 /home/pi/TrailerAutomationNet/TrailerAutomationClientNet


echo "Starting service..."
sudo systemctl start  trailer-automation-client
sleep 2
sudo systemctl status trailer-automation-client

echo ""
echo "Deployment complete!"
echo "Watching logs (Ctrl+C to exit)..."
sudo journalctl -u trailerautomation-client -f
