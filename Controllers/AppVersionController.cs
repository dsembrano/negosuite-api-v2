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
    private readonly AmazonS3Client _s3Client;
    private readonly string SpaceName;

    private readonly negosuiteContext _context;
    private IConfiguration _config { get; }

    public AppVersionController(negosuiteContext context, IConfiguration configuration)
    {
        _context = context;
        _config = configuration;

        var accessKey = configuration["Storage:AccessKey"] ?? throw new InvalidOperationException("Storage:AccessKey is required for APK downloads.");
        var secretKey = configuration["Storage:SecretKey"] ?? throw new InvalidOperationException("Storage:SecretKey is required for APK downloads.");
        var region = configuration["Storage:Region"] ?? throw new InvalidOperationException("Storage:Region is required for APK downloads.");
        SpaceName = configuration["Storage:Bucket"] ?? throw new InvalidOperationException("Storage:Bucket is required for APK downloads.");
        var config = new AmazonS3Config
        {
            ServiceURL = $"https://{region}.digitaloceanspaces.com", // DigitalOcean Spaces URL
            ForcePathStyle = true
        };
        _s3Client = new AmazonS3Client(accessKey, secretKey, config);
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
