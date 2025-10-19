namespace YouTubester.Application;

public interface IVideoTemplatingService
{
    Task<CopyVideoTemplateResult> CopyTemplateAsync(CopyVideoTemplateRequest request,
        CancellationToken cancellationToken);
}