using Amazon;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using negosuite_api.Models;
using System;
using System.IO;
using System.Net.Mime;
using System.Threading.Tasks;

[ApiController]
public class AppVersionController : ControllerBase
{
    private const string AccessKey = "DO801LFQ3GCV966MRJFA";
    private const string SecretKey = "r5z9/QqzVqDkKHsQC0kuqlXPc63mI2dqyejo0w4v7LA";
    private const string SpaceName = "negosuite-app";
    private const string Region = "sgp1";
    private readonly AmazonS3Client _s3Client;

    private readonly negosuiteContext _context;
    private IConfiguration _config { get; }

    public AppVersionController(negosuiteContext context, IConfiguration configuration)
    {
        _context = context;
        _config = configuration;

        var config = new AmazonS3Config
        {
            ServiceURL = $"https://{Region}.digitaloceanspaces.com", // DigitalOcean Spaces URL
            ForcePathStyle = true
        };
        _s3Client = new AmazonS3Client(AccessKey, SecretKey, config);
    }


    [HttpGet]
    [Route("api/download-apk")]
    public async Task<IActionResult> DownloadApk()
    {
        var appVersion = await _context.AppVersions.FindAsync(1);

        string originalFileName = $"apk-release/{appVersion.APKFilename}"; 
        string newFileName = $"com.negosuite.app-release.1.0.4-{DateTime.UtcNow:yyyyMMddHHmmss}.apk";

        try
        {
            // Fetch file from DigitalOcean Space
            var request = new GetObjectRequest
            {
                BucketName = SpaceName,
                Key = originalFileName
            };

            using (var response = await _s3Client.GetObjectAsync(request))
            using (var memoryStream = new MemoryStream())
            {
                await response.ResponseStream.CopyToAsync(memoryStream);
                memoryStream.Position = 0; // Reset stream position

                // Return the file as a downloadable response
                return File(memoryStream.ToArray(),
                            "application/vnd.android.package-archive",
                            newFileName);
            }
        }
        catch (AmazonS3Exception ex)
        {
            return BadRequest($"Error retrieving file: {ex.Message}");
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Internal server error: {ex.Message}");
        }
    }
}
