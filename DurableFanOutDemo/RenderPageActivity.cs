using Microsoft.Azure.Functions.Worker;

namespace DurableFanOutDemo;

// The memory before/after from the article. The "broken" form returns the whole
// payload, which gets serialized into orchestration history on fan-in. The "fixed"
// form writes the payload to blob storage and returns a small reference instead.
//
// Only the FIXED form is registered as a Function (two [Function] attributes with the
// same name would collide); the broken form is kept as a plain method for contrast.
public record RenderedPage(int Page, byte[] Image);
public record PageRef(int Page, string BlobName);

public static class RenderPageActivity
{
    // BROKEN: the multi-MB image is serialized into history on fan-in.
    public static RenderedPage RunBroken([ActivityTrigger] int page)
    {
        byte[] image = Renderer.ToPng(page);     // multi-MB payload
        return new RenderedPage(page, image);    // every byte lands in history
    }

    // FIXED: write the payload to blob storage, return a small reference.
    [Function(nameof(RenderPageActivity))]
    public static async Task<PageRef> Run([ActivityTrigger] int page)
    {
        byte[] image = Renderer.ToPng(page);
        string blobName = await Blobs.UploadAsync($"pages/{page}.png", image);
        return new PageRef(page, blobName);      // history carries a reference, not the bytes
    }
}

// Stubbed helpers so the before/after compiles. In a real app Renderer produces the
// payload and Blobs wraps the Storage.Blobs SDK; here they are stand-ins that compile
// without a live storage account.
internal static class Renderer
{
    public static byte[] ToPng(int page) => new byte[1];
}

internal static class Blobs
{
    public static Task<string> UploadAsync(string blobName, byte[] bytes) =>
        Task.FromResult(blobName);
}
