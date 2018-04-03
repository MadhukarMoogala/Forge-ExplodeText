# Forge-ExplodeText 
[![OAuth2](https://img.shields.io/badge/OAuth2-v1-green.svg)](http://developer.autodesk.com/)
[![DesignAutomation](https://img.shields.io/badge/Design%20Automation%20V2--green.svg)](https://developer.autodesk.com/en/docs/design-automation/v2)
![Shippable](https://img.shields.io/badge/VS2017--brightgreen.svg)
![Shippable](https://img.shields.io/badge/.NET-4.6-brightgreen.svg)

# Setup

For using this sample, you need an Autodesk developer credentials. Visit the [Forge Developer Portal](https://developer.autodesk.com), sign up for an account, then [create an app](https://developer.autodesk.com/myapps/create) that uses Design Automation. For this new app, use `http://localhost:3000/api/forge/callback/oauth` as Callback URL [This is option, we are not developing UI], although is not used on 2-legged flow. Finally take note of the **Client ID** and **Client Secret**.

## Run Locally

Development Enviroment VS 2017 Community or Professional or Ultimate.
.NET framework 4.6

Open the **app.config** file and adjust the Forge Client ID & Secret. If you plan to deploy to Appharbor, configure the variables (no need to change this web.config file).

```xml
<appSettings>
  <add key="FORGE_CLIENT_ID" value="" />
  <add key="FORGE_CLIENT_SECRET" value="" />
</appSettings>
```
# License

This sample is licensed under the terms of the [MIT License](http://opensource.org/licenses/MIT).
Please see the [LICENSE](LICENSE) file for full details.


# Preview

![Alt Text](https://media.giphy.com/media/WgO7yxtbgO08AqwEKq/giphy.gif)
