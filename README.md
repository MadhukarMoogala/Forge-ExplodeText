# Forge-ExplodeText 
[![OAuth2](https://img.shields.io/badge/OAuth2-v1-green.svg)](http://developer.autodesk.com/)
[![DesignAutomation](https://img.shields.io/badge/Design%20Automation%20V2--green.svg)](https://developer.autodesk.com/en/docs/design-automation/v2)
![Shippable](https://img.shields.io/badge/VS2017--brightgreen.svg)
![Shippable](https://img.shields.io/badge/.NET-4.6-brightgreen.svg)

# Setup

For using this sample, you need an Autodesk developer credentials. Visit the [Forge Developer Portal](https://developer.autodesk.com), sign up for an account, then [create an app](https://developer.autodesk.com/myapps/create) that uses Design Automation. For this new app, use `http://localhost:3000/api/forge/callback/oauth` as Callback URL [This is option, we are not developing UI], although is not used on 2-legged flow. Finally take note of the **Client ID** and **Client Secret**.

## Run Locally

Development Enviroment VS 2017 Community or Professional or Ultimate.
.NET framework 4.6.

```bash
Launch VS 2017 Developer Command from start menu
git clone https://github.com/MadhukarMoogala/Forge-ExplodeText.git
cd Forge-ExplodeText
devenv .\FDA.ExplodeText\FDA.ExplodeText.sln
```
There is only a single variable [drawingResource](https://github.com/MadhukarMoogala/Forge-ExplodeText/blob/b483647d52649d48833a32e35211e3e6ff2d6dd1/FDA.ExplodeText/Program.cs#L269) that needs to be customized to provide your own drawing with DBTEXT entities.

Please ensure provided drawing resource URL should be accessible by HTTP. This sample fetches drawing resource from [Amazon S3 Bucket](https://docs.aws.amazon.com/AmazonS3/latest/dev/UsingBucket.html).

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

[![EXPTXT1.gif](https://s1.gifyu.com/images/EXPTXT1.gif)](https://gifyu.com/image/sRRt)

