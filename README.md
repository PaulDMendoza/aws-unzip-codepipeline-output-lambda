# aws-unzip-codepipeline-output-lambda
This takes a single build artifact from AWS CodePipline and unzips the artifacts in the S3 folder right next to the output with the postfix _output. Deploy as a Lambda function.

Why I built this?
-------
AWS CodePipeline zips up the build artifacts after running CodeBuild which is difficult to deal with. So if you need to do something with your artifacts in later steps you will probably need to unzip them to an S3.

What it does
---------------
Receives the CodePipeline event, gets the first Input Artifact which is stored in AWS S3 and unzips the files to the artificat path + "_output". 

**Output Example**
If the artifact path for the zip file was 
```
mybucket/artifacts/323J323
```

Then the folder where the unzipped files are placed is

```
mybucket/artifacts/323J323_output/
```


How To Use This
---------------

- Build this using dotnet core 2.0. (You might need to download it if you don't have it.)

```
dotnet lambda package -c Release -f netcoreapp2.0 dragnet-website.zip
```

- Deploy the Lambda to AWS. Probably easiest to just upload the compiled version.
    - Be sure to allocate enough RAM for the files. This currently uses RAM and no disk to extract files so all the files need to be able to fit into RAM. 
	- The function is named "ExtractFiles"
- In the CodePipeline after the Build, define an Invoke command to call this Lambda expression.
- Later on in my CloudFormation step, I setup the following in the Advanced -> Parameter Overrrides

```
{
"LambdaS3Bucket": { "Fn::GetArtifactAtt" : [ "MyAppBuild", "BucketName" ] },
"LambdaS3ZipFile": { "Fn::GetArtifactAtt" : ["MyAppBuild", "ObjectKey"]} 
}
```


- How I used the outputs: In my CloudFormation template where I need to reference a Zip file in my artifacts for another Lambda I wanted to deploy, I used this to combine the parts.

```
    ...
    "Code": {
        "S3Bucket": { "Ref" : "LambdaS3Bucket" },
        "S3Key": { "Fn::Sub" : [ "${prefix}_output/my-awesome-app.zip", { "prefix": { "Ref" : "LambdaS3ZipFile" } } ] } 
    },
    ...
```


Move Files to S3 Bucket
---------------------
I also needed to then move those files to an S3 bucket so there is another function embedded in this which will take the output of the ExtractFiles function and move some of the files to a public S3 bucket. 

Here is an example defintion of the function.

```
{
    "profile"     : "default",
    "region"      : "us-west-2",
    "configuration" : "Release",
    "framework"     : "netcoreapp2.0",
    "function-runtime" : "dotnetcore2.0",
    "function-memory-size" : 384,
    "function-timeout"     : 120,
    "function-handler"     : "aws-unzip-codepipeline-output-lambda::ExtractStaticFiles.Function::CopyArtifactExtractedFiles",
    "function-name"        : "CopyFilesToS3Bucket",
    "function-role"        : "arn:aws:iam::354135755476:role/MyRole",
    "environment-variables" : ""
}

```









