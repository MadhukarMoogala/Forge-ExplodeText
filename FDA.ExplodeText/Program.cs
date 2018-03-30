using Amazon.S3;
using Amazon.S3.Model;
using FDA.ExplodeText.ACES.Models;
using FDA.ExplodeText.Operations;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace FDA.ExplodeText
{
    class Credentials
    {
        /// <summary>
        /// Reads appsettings from App.config
        /// </summary>
        private static string GetAppSetting(string settingKey)
        {
            var appSettings = ConfigurationManager.AppSettings;
            return appSettings[settingKey];
        }
        //get your ConsumerKey/ConsumerSecret at http://developer.autodesk.com         
        public static string ForgeConsumerKey = Environment.GetEnvironmentVariable("FORGE_CLIENT_ID") ?? GetAppSetting("FORGE_CLIENT_ID");
        public static string ForgeConsumerSecret = Environment.GetEnvironmentVariable("FORGE_CLIENT_SECRET") ?? GetAppSetting("FORGE_CLIENT_SECRET");
        public static string AWSAccessKey = Environment.GetEnvironmentVariable("AWSACCESSKEY") ?? GetAppSetting("AWSACCESSKEY");
        public static string AWSSecretKey = Environment.GetEnvironmentVariable("AWSSECRETKEY") ?? GetAppSetting("AWSSECRETKEY");

    }
    class Program
    {
        static readonly string ActivityName = "EXPTXT";
        static readonly string PackageName = "ExplodeText";
        static Container container;
        static void Main(string[] args)
        {
            //instruct client side library to insert token as Authorization value into each request
            container = new Container(new Uri("https://developer.api.autodesk.com/autocad.io/us-east/v2/"));
            var token = GetToken();
            container.SendingRequest2 += (sender, e) => e.RequestMessage.SetHeader("Authorization", token);

            //check if our app package exists
            AppPackage package = null;
            var packageQ = container.AppPackages.ByKey(PackageName);
            try { package = packageQ.GetValue(); } catch { }
            string res = "New";
            if (package != null)
            {
                res = PromptVersions(package.Version, packageQ.GetVersions().RequestUri,
                    packageQ.SetVersion(0).RequestUri, token, PackageName);
            }
            if (res == "New")
                package = CreateOrUpdatePackage(CreateZip(), package);

            //check if our activity already exists
            Activity activity = null;
            var activityQ = container.Activities.ByKey(ActivityName);
            try { activity = activityQ.GetValue(); }
            catch { }
            res = "New";
            if (activity != null)
            {
                res = PromptVersions(package.Version, activityQ.GetVersions().RequestUri,
                    activityQ.SetVersion(0).RequestUri, token, ActivityName);
            }
            if (res == "New")
                activity = CreateOrUpdateActivity(activity, PackageName);

            //save outstanding changes if any
            container.SaveChanges();

            //finally submit workitem against our activity
            SubmitWorkItem(activity);
        }

        static Activity CreateOrUpdateActivity(Activity activity, string packageName)
        {

            Console.WriteLine("Creating/Updating Activity...");
            bool newlyCreated = false;
            if (activity == null)
            {
                activity = new Activity() { Id = ActivityName };
                newlyCreated = true;
            }

            activity.Instruction = new Instruction()
            {
                //Save the drawing to AutoCAD 2018 Format after Exploding all DBText entities in Given Drawing
                Script = "EXPTXT\nSAVEAS\n2018\nresult.dwg\nQUIT\n"
            };
            activity.Parameters = new Parameters()
            {
                InputParameters = {
                        new Parameter() { Name = "HostDwg", LocalFileName = "$(HostDwg)" },
                    },
                OutputParameters = { new Parameter() { Name = "Result", LocalFileName = "result.dwg" } }
            };
            activity.RequiredEngineVersion = "22.0";
            if (newlyCreated)
            {
                activity.AppPackages.Add(packageName); // reference the custom AppPackage
                container.AddToActivities(activity);
            }
            else
                container.UpdateObject(activity);
            container.SaveChanges();
            return activity;

        }

        static string GetToken()
        {
            Console.WriteLine("Getting authorization token...");
            using (var client = new HttpClient())
            {
                var values = new List<KeyValuePair<string, string>>
                {
                    new KeyValuePair<string, string>("client_id", Credentials.ForgeConsumerKey),
                    new KeyValuePair<string, string>("client_secret", Credentials.ForgeConsumerSecret),
                    new KeyValuePair<string, string>("grant_type", "client_credentials"),
                    new KeyValuePair<string, string>("scope", "code:all")
                };
                var requestContent = new FormUrlEncodedContent(values);
                var response = client.PostAsync("https://developer.api.autodesk.com/authentication/v1/authenticate", requestContent).Result;
                var responseContent = response.Content.ReadAsStringAsync().Result;
                var resValues = JsonConvert.DeserializeObject<Dictionary<string, string>>(responseContent);
                return resValues["token_type"] + " " + resValues["access_token"];
            }
        }

        static string PromptVersions(int curVer, Uri getVersions, Uri setVersion, string token)
        {
            // We have multiple versions of the is package already, retrieve all versions and show the current
            //Console.WriteLine("'{0}' already exists. The following versions are available:", PackageName);
            int min = int.MaxValue;
            int max = int.MinValue;

            var cont = new Container(new Uri("https://developer.api.autodesk.com/autocad.io/us-east/v2/"));
            cont.SendingRequest2 += (sender, e) => e.RequestMessage.SetHeader("Authorization", token);

            foreach (dynamic item in cont.Execute<dynamic>(getVersions))
            {
                if (item.Version > max)
                    max = item.Version;
                if (item.Version < min)
                    min = item.Version;
                Console.WriteLine("Version #: {0}.Time Submitted: {1}.", item.Version, item.Timestamp);
            }
            Console.WriteLine("Current={0}", curVer);
            var res = Prompts.PromptForKeyword("What do you want to do? [New/SetCurrent/Leave]<New>");
            if (res == "SetCurrent")
            {
                var ver = Prompts.PromptForNumber("Choose a version", min, max);
                container.Execute(setVersion, "POST", new Microsoft.OData.Client.BodyOperationParameter("Version", ver));
            }
            return res;
        }
        static string PromptVersions(int curVer, Uri getVersions, Uri setVersion, string token, string name)
        {
            // We have multiple versions of the is package already, retrieve all versions and show the current
            Console.WriteLine("'{0}' already exists. The following versions are available:", name);
            int min = int.MaxValue;
            int max = int.MinValue;

            var cont = new Container(new Uri("https://developer.api.autodesk.com/autocad.io/us-east/v2/"));
            cont.SendingRequest2 += (sender, e) => e.RequestMessage.SetHeader("Authorization", token);

            foreach (dynamic item in cont.Execute<dynamic>(getVersions))
            {
                if (item.Version > max)
                    max = item.Version;
                if (item.Version < min)
                    min = item.Version;
                Console.WriteLine("Version #: {0}.Time Submitted: {1}.", item.Version, item.Timestamp);
            }
            Console.WriteLine("Current={0}", curVer);
            var res = Prompts.PromptForKeyword("What do you want to do? [New/SetCurrent/Leave]<New>");
            if (res == "SetCurrent")
            {
                var ver = Prompts.PromptForNumber("Choose a version", min, max);
                container.Execute(setVersion, "POST", new Microsoft.OData.Client.BodyOperationParameter("Version", ver));
            }
            return res;
        }



        //creates an activity with 
        static AppPackage CreateOrUpdatePackage(string zip, AppPackage package)
        {
            Console.WriteLine("Creating/Updating AppPackage...");
            // First step -- query for the url to upload the AppPackage file
            var url = container.AppPackages.GetUploadUrl().GetValue();

            // Second step -- upload AppPackage file
            UploadObject(url, zip);

            if (package == null)
            {
                // third step -- after upload, create the AppPackage entity
                package = new AppPackage()
                {
                    Id = PackageName,
                    RequiredEngineVersion = "22.0",
                    Resource = url
                };
                container.AddToAppPackages(package);
            }
            else
            {
                //or update the existing one with the new url
                package.Resource = url;
                container.UpdateObject(package);
            }
            container.SaveChanges();
            return package;
        }

        static string CreateZip()
        {
            //For use this, zip program.
            //put bundle directory in same directory as current executable resides.
            //

            /*
             * ─ExplodeText.bundle
                        │---PackageContents.xml
                        └───Contents
                               |
                               - main.dll
             */
           //Zip package has to pack the base directory i.e., ExploteText.Bundle too

            Console.WriteLine("Generating autoloader zip...");
            string zip = "package.zip";
            if (File.Exists(zip))
                File.Delete(zip);
            string bundle = PackageName + ".bundle";
            bundle = Path.GetFullPath(bundle);
            string startPath = bundle;
            ZipFile.CreateFromDirectory(startPath, zip,CompressionLevel.Fastest,true);
            return zip;

        }
        static void UploadObject(string url, string filePath)
        {
            Console.WriteLine("Uploading autoloader zip...");
            var client = new HttpClient();
            client.PutAsync(url, new StreamContent(File.OpenRead(filePath))).Result.EnsureSuccessStatusCode();
        }

        static void SubmitWorkItem(Activity activity)
        {
            Console.WriteLine("Submitting workitem...");
            //create a workitem
            var wi = new WorkItem()
            {
                Id = "", //must be set to empty
                Arguments = new Arguments(),
                ActivityId = activity.Id
            };

            string drawingResource = GeneratePreSignedURL("madhukar-fda", "TestDrawing.dwg");
            wi.Arguments.InputArguments.Add(new Argument()
            {
                Name = "HostDwg",// Must match the input parameter in activity
                Resource = drawingResource,
                StorageProvider = StorageProvider.Generic //Generic HTTP download (as opposed to A360)
            });
           
            wi.Arguments.OutputArguments.Add(new Argument()
            {
                Name = "Result", //must match the output parameter in activity
                StorageProvider = StorageProvider.Generic, //Generic HTTP upload (as opposed to A360)
                HttpVerb = HttpVerbType.POST, //use HTTP POST when delivering result
                Resource = null, //use storage provided by AutoCAD.IO

            });

            container.AddToWorkItems(wi);
            container.SaveChanges();

            //polling loop
            do
            {
                Console.WriteLine("Sleeping for 2 sec...");
                System.Threading.Thread.Sleep(2000);
                container.LoadProperty(wi, "Status"); //http request is made here
                Console.WriteLine("WorkItem status: {0}", wi.Status);
            }
            while (wi.Status == ExecutionStatus.Pending || wi.Status == ExecutionStatus.InProgress);

            //re-query the service so that we can look at the details provided by the service
            container.MergeOption = Microsoft.OData.Client.MergeOption.OverwriteChanges;
            wi = container.WorkItems.ByKey(wi.Id).GetValue();

            //download the status report
            var url = wi.StatusDetails.Report;
            DownloadToDocs(url, @"output-report.txt");

            //Resource property of the output argument "Results" will have the output url
            url = wi.Arguments.OutputArguments.First(a => a.Name == "Result").Resource;
            DownloadToDocs(url, @"AfterExplode.dwg");

        }

        static void DownloadToDocs(string url, string localFile)
        {
            if(String.IsNullOrEmpty(url))
            {
                Console.WriteLine("Downloading to failed due to bad url");
            }
            var client = new HttpClient();
            var content = (StreamContent)client.GetAsync(url).Result.Content;
            var fname = Path.Combine(Environment.CurrentDirectory, localFile);
            Console.WriteLine("Downloading to {0}.", fname);
            using (var output = System.IO.File.Create(fname))
            {
                content.ReadAsStreamAsync().Result.CopyTo(output);
                output.Close();
            }
        }

        static string GeneratePreSignedURL(string bucketName, string objectKey)
        {
            string urlString = "";
            GetPreSignedUrlRequest request1 = new GetPreSignedUrlRequest
            {
                BucketName = bucketName,
                Key = objectKey,
                Verb = HttpVerb.GET,
                Expires = DateTime.Now.AddMinutes(60)

            };

            try
            {
                using (var s3Client = new AmazonS3Client(Amazon.RegionEndpoint.USWest2))
                {
                    urlString = s3Client.GetPreSignedURL(request1);
                }
                //string url = s3Client.GetPreSignedURL(request1);
            }
            catch (AmazonS3Exception amazonS3Exception)
            {
                if (amazonS3Exception.ErrorCode != null &&
                    (amazonS3Exception.ErrorCode.Equals("InvalidAccessKeyId")
                    ||
                    amazonS3Exception.ErrorCode.Equals("InvalidSecurity")))
                {
                    Console.WriteLine("Check the provided AWS Credentials.");
                    Console.WriteLine(
                    "To sign up for service, go to http://aws.amazon.com/s3");
                }
                else
                {
                    Console.WriteLine(
                     "Error occurred. Message:'{0}' when listing objects",
                     amazonS3Exception.Message);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }

            return urlString;


        }

    }
}
