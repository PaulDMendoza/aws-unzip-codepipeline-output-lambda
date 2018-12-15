using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace ExtractStaticFiles
{
    public class Configuration
    {
        public string FunctionName { get; set; }
        public string UserParameters { get; set; }
    }

    public class ActionConfiguration
    {
        public Configuration configuration { get; set; }
    }

    public class S3Location
    {
        public string objectKey { get; set; }
        public string bucketName { get; set; }
    }

    public class Location
    {
        public string type { get; set; }
        public S3Location s3Location { get; set; }
    }

    public class Artifact
    {
        public Location location { get; set; }
        public object revision { get; set; }
        public string name { get; set; }
    }

    public class ArtifactCredentials
    {
        public string sessionToken { get; set; }
        public string secretAccessKey { get; set; }
        public string accessKeyId { get; set; }
    }

    public class Data
    {
        public ActionConfiguration actionConfiguration { get; set; }
        public List<Artifact> inputArtifacts { get; set; }
        public List<Artifact> outputArtifacts { get; set; }
        public ArtifactCredentials artifactCredentials { get; set; }
    }

    public class CodePipelineJob
    {
        public string id { get; set; }
        public string accountId { get; set; }
        public Data data { get; set; }
    }

    public class CodePipelineEvent
    {
        [JsonProperty("CodePipeline.job")]
        public CodePipelineJob Job { get; set; }
    }
}
