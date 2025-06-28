# UniFi Plugin for InfoPanel

InfoPanel UniFi API reader plugin to get upload speed, download speed und some more informations directly from your gateway.

## Installation and Setup
Follow these steps to get the UniFi plugin working with InfoPanel:

1. **Download the plugin**:
   - Download the latest release \*.zip file (`UniFiPlugin-vX.X.X.zip`) from the [GitHub Releases page](https://github.com/markuskonojacki/InfoPanel.UniFi/releases).

2. **Import the Plugin into InfoPanel**:
   - Open the InfoPanel app.
   - Navigate to the **Plugins** page.
   - Click **Import Plugin Archive**, then select the downloaded ZIP file.
   - InfoPanel will extract and install the plugin.

3. **Configure the Plugin**:
   - On the Plugins page, click **Open Plugins Folder** to locate the plugin files.
   - Close InfoPanel.
   - Open `InfoPanel.UniFi.dll.ini` in a text editor (e.g., Notepad).
   - Fill in the information as needed.
     - Everything here is valid if your gateway has the IP 192.168.1.1, if not change it in the next steps accordingly and in the InfoPanel.UniFi.dll.ini
     - You can generate the API key at https://192.168.1.1/network/default/settings/control-plane/integrations
     - Site name is normally `default` and if you only got one WAN port active the WANNumber should be `0`
     - If not, you can get the informations here: https://192.168.1.1/proxy/network/v2/api/site/default/aggregated-dashboard?historySeconds=3
   - Save and close the file.

## Configuration example
- **`InfoPanel.UniFi.dll.ini`**:
  ```ini
  [UniFi Plugin]
  ControllerURL = https://192.168.1.1
  APIKey = <insert-your-api-key>
  SiteName = default
  WANNumber = 0
  ```
