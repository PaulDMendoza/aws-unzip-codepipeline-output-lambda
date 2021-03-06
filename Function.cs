using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Amazon.Lambda.Core;
using Amazon.Lambda.S3Events;
using Amazon.S3;
using Amazon.S3.Util;
using System.IO.Compression;
using System.IO;
using Amazon.Runtime;
using Newtonsoft.Json;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

namespace ExtractStaticFiles
{
    public class Function
    {
        IAmazonS3 S3Client { get; set; }
        Amazon.CodePipeline.IAmazonCodePipeline CodePipelineClient { get; set; }

        /// <summary>
        /// Default constructor. This constructor is used by Lambda to construct the instance. When invoked in a Lambda environment
        /// the AWS credentials will come from the IAM role associated with the function and the AWS region will be set to the
        /// region the Lambda function is executed in.
        /// </summary>
        public Function()
        {
            S3Client = new AmazonS3Client();
            CodePipelineClient = new Amazon.CodePipeline.AmazonCodePipelineClient();
        }

        /// <summary>
        /// Constructs an instance with a preconfigured S3 client. This can be used for testing the outside of the Lambda environment.
        /// </summary>
        /// <param name="s3Client"></param>
        public Function(IAmazonS3 s3Client)
        {
            this.S3Client = s3Client;
        }

        /// <summary>
        /// This method is called for every Lambda invocation. This method takes in an S3 event object and can be used 
        /// to respond to S3 notifications.
        /// </summary>
        /// <param name="evnt"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        public async Task<string> ExtractFiles(CodePipelineEvent evnt, ILambdaContext context)
        {
            Console.WriteLine(Newtonsoft.Json.JsonConvert.SerializeObject(evnt, Newtonsoft.Json.Formatting.Indented));

            var artifact = evnt.Job.data.inputArtifacts.First();

            //var credentials = new BasicAWSCredentials(evnt.Job.data.artifactCredentials.accessKeyId, evnt.Job.data.artifactCredentials.secretAccessKey);
            //IAmazonS3 s3PipelineArtifactAccess = new AmazonS3Client(credentials);


            using (var objectStream = await S3Client.GetObjectStreamAsync(artifact.location.s3Location.bucketName, artifact.location.s3Location.objectKey, new Dictionary<string, object>()))
            {
                var memoryStream = new MemoryStream();
                objectStream.CopyTo(memoryStream);

                context.Logger.LogLine("Fetched object stream async, Bucket: " + artifact.location.s3Location.bucketName);
                using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Read))
                {
                    context.Logger.LogLine("About to read entries");
                    foreach (var entry in archive.Entries)
                    {
                        context.Logger.LogLine("File: " + entry.FullName);
                        using (var fileStream = entry.Open())
                        {
                            var fileMemStream = new MemoryStream();
                            fileStream.CopyTo(fileMemStream);
                            fileMemStream.Position = 0;

                            var fileKey = artifact.location.s3Location.objectKey + "_output/" + entry.FullName;
                            context.Logger.LogLine(fileKey);
                            await this.S3Client.UploadObjectFromStreamAsync(artifact.location.s3Location.bucketName, fileKey, fileMemStream, additionalProperties: null);
                            
                        }
                    }
                }
            }

            await CodePipelineClient.PutJobSuccessResultAsync(new Amazon.CodePipeline.Model.PutJobSuccessResultRequest
            {
                JobId = evnt.Job.id
            });

            return "Success";
        }


        public class ArtifactsToCopy
        {
            public string SourcePrefix { get; set; }
            public string DestBucket { get; set; }
            public string DestPrefix { get; set; }
        }

        /// <summary>
        /// The only artifact should be the one passed to the above function.
        /// </summary>
        /// <param name="evnt"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        public async Task<string> CopyArtifactExtractedFiles(CodePipelineEvent evnt, ILambdaContext context)
        {
            context.Logger.LogLine(Newtonsoft.Json.JsonConvert.SerializeObject(evnt, Newtonsoft.Json.Formatting.Indented));

            var artifact = evnt.Job.data.inputArtifacts.First();


            var toCopy = JsonConvert.DeserializeObject<ArtifactsToCopy>(evnt.Job.data.actionConfiguration.configuration.UserParameters);

            var outputFolderFromPriorStep = artifact.location.s3Location.objectKey + "_output/";

            var filePrefix = outputFolderFromPriorStep + toCopy.SourcePrefix;


            var objects = await S3Client.ListObjectsV2Async(new Amazon.S3.Model.ListObjectsV2Request
            {
                BucketName = artifact.location.s3Location.bucketName,
                Prefix = filePrefix,
                MaxKeys = 100000
            });
            context.Logger.LogLine("Objects: Keys=" + objects.KeyCount + " NextConinuationToken: " + objects.NextContinuationToken);

            foreach(var o in objects.S3Objects)
            {
                context.Logger.LogLine("File: " + o.Key);
                await S3Client.CopyObjectAsync(artifact.location.s3Location.bucketName, o.Key, toCopy.DestBucket, o.Key.Replace(filePrefix, ""));
            }

            context.Logger.LogLine("Call PutJobSuccessResultAsync, JobID: " + evnt.Job.id);
            await CodePipelineClient.PutJobSuccessResultAsync(new Amazon.CodePipeline.Model.PutJobSuccessResultRequest
            {
                JobId = evnt.Job.id
            });

            context.Logger.LogLine("Completed");

            return "Success";
        }

    }
}
