OohLaLog IIS Logging Library
------------------------------------

# Using the OohLaLog IIS appender

The OohLaLog IIS appender is an Http Module that can be configured for use in websites running in IIS 7+.
 
### Usage

Include the OohLaLogIISAppender library into your .NET project. The library DLL (OohLaLogIISAppender.dll) can be found in the root of this repository.

To configure IIS appender for use in a website, OohLaLog settings need to be set in a separate <oohlalog> configSection and activated as an Http Module 
in the <system.webServer> modules section:

Logging levels correspond to Http Status Codes as follows:
INFO: Http Status Codes of 100-399
WARN: Http Status Codes of 400-499
ERROR: Http Status Codes of 500-599


```html
  <!-- This section contains the oohlalog configuration settings -->
  <configSections>
    <section name="oohlalog" type="System.Configuration.NameValueSectionHandler" />
  </configSections>
  <oohlalog>
    <!--required settings-->
    <add key="apikey" value="--YOUR_API_KEY_HERE--"/>
    <!--optional settings:
      issecure: true or false; default is false
      hostname: default is machine name as returned from System.Environment.MachineName
      loglevel: "ALL","DEBUG","INFO","WARN","ERROR"; default is "ALL"
      bufferlimit: number of logs to buffer before posting to OLL (lower numbers impact app performance); default is 150
      bufferinterval: age in seconds of logs in buffer before automatic posting to OLL (lower numbers impact app performance); default is 60 seconds
    <add key="issecure" value="false"/>
    <add key="hostname" value="test"/>
    <add key="loglevel" value="DEBUG"/>
    <add key="bufferlimit" value="150"/>
    <add key="bufferinterval" value="60"/>
    -->
  </oohlalog>


  <!-- This section contains webServer configuration to include http modules -->
  <system.webServer>
    <modules>
      <add name="OohLaLog.IISAppender" type="OohLaLog.IISAppender" />
    </modules>
  </system.webServer>

```
