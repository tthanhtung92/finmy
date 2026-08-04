using Finmy.Budgeting.Application.Receipts;
using Finmy.Modularity.Extensions;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Finmy.Budgeting.Api.Endpoints;

public static class ReceiptEndpoints
{
    public static void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/receipts");

        group.MapPost("/", UploadReceiptAsync)
            .DisableAntiforgery();

        group.MapGet("/{id:guid}", GetReceiptAsync);
    }

    private static async Task<IResult> UploadReceiptAsync(
        IFormFile file,
        ReceiptService svc,
        CancellationToken cancellationToken)
    {
        using var stream = file.OpenReadStream();
        var result = await svc.UploadAsync(stream, file.Length, file.ContentType, file.FileName, cancellationToken);
        return result.Match(x => Results.Created($"/receipts/{x.Id}", x));
    }

    private static async Task<IResult> GetReceiptAsync(
        Guid id,
        ReceiptService svc,
        HttpResponse response,
        CancellationToken cancellationToken)
    {
        var result = await svc.GetForServingAsync(id, cancellationToken);
        return result.Match(url =>
        {
            response.Headers.CacheControl = "private, no-store";
            return Results.Redirect(url, permanent: false);
        });
    }
}