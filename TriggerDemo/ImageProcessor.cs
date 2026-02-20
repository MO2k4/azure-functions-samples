using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace TriggerDemo;

public class ImageProcessor(ILogger<ImageProcessor> logger)
{
    // Watches the "uploads" container. Output goes to "thumbnails" — never the same
    // container as the trigger, or you create an infinite loop.
    [Function(nameof(ImageProcessor))]
    [BlobOutput("thumbnails/{name}", Connection = "AzureWebJobsStorage")]
    public byte[] Run(
        [BlobTrigger("uploads/{name}", Connection = "AzureWebJobsStorage")]
        byte[] imageData,
        string name)
    {
        logger.LogInformation("Processing uploaded image: {Name} ({Size} bytes)",
            name, imageData.Length);

        return GenerateThumbnail(imageData);
    }

    private static byte[] GenerateThumbnail(byte[] imageData)
    {
        // Replace with real thumbnail generation (e.g., ImageSharp, SkiaSharp).
        return imageData;
    }
}
