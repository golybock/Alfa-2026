using System.Globalization;
using System.Text.Json;
using AlfaNews.Api.Access;
using AlfaNews.Contracts.Api;
using AlfaNews.Contracts.Domain;
using AlfaNews.Contracts.Processing;
using AlfaNews.Core.Access;
using AlfaNews.Core.Data;
using AlfaNews.Core.Documents;
using AlfaNews.Core.Ingest;
using AlfaNews.Core.Processing;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;

namespace AlfaNews.Api.Documents;

public static class DocumentEndpoints
{
    public static void MapDocumentEndpoints(this WebApplication app)
    {
        // Загрузка и список материалов открыты всем вошедшим (AccessService.CanAddMaterials, демонстрация), проверка страниц — только оператору.
        var group = app.MapGroup("/api/admin/documents").RequireAuthorization();
        group.MapPost("", async (HttpContext http, AccessService access, PdfRegistration registration, CancellationToken cancellationToken) =>
        {
            var user = SessionAuthenticationHandler.CurrentUser(http.User);
            await access.RequireMaterialsAsync(user, cancellationToken);
            if (!http.Request.HasFormContentType)
                throw new AccessException(415, "invalid_input", "Требуется multipart/form-data.");
            IFormCollection form;
            try
            {
                form = await http.Request.ReadFormAsync(cancellationToken);
            }
            catch (InvalidDataException)
            {
                throw new AccessException(400, "invalid_input", "Некорректная форма загрузки.");
            }
            var file = form.Files.GetFile("file");
            if (file is null || form.Files.Count != 1 || file.ContentType != "application/pdf")
                throw new AccessException(415, "invalid_input", "Требуется один файл PDF.");
            if (file.Length > PdfOptions.MaxFileBytes)
                throw new AccessException(413, "invalid_input", "PDF превышает 50 MiB.");
            DateOnly? date = null;
            var dateText = form["published_on"].ToString();
            if (dateText.Length > 0)
                date = DateOnly.TryParseExact(dateText, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
                    ? parsed : throw new AccessException(400, "invalid_input", "Некорректная дата публикации.");
            await using var input = file.OpenReadStream();
            var result = await registration.RegisterAsync(input, form["title"].ToString(), date,
                string.IsNullOrWhiteSpace(form["source_url"]) ? null : form["source_url"].ToString(), user.UserId, cancellationToken);
            return Results.Accepted($"/api/admin/documents/{result.DocumentId}", result);
        }).AddOpenApiOperationTransformer((operation, _, _) =>
        {
            operation.RequestBody = new OpenApiRequestBody
            {
                Required = true,
                Content = new Dictionary<string, OpenApiMediaType>
                {
                    ["multipart/form-data"] = new()
                    {
                        Schema = new OpenApiSchema
                        {
                            Type = JsonSchemaType.Object,
                            Required = new HashSet<string> { "file", "title" },
                            Properties = new Dictionary<string, IOpenApiSchema>
                            {
                                ["file"] = new OpenApiSchema { Type = JsonSchemaType.String, Format = "binary", Description = "PDF до 50 MiB и 50 страниц." },
                                ["title"] = new OpenApiSchema { Type = JsonSchemaType.String, Description = "Название документа." },
                                ["published_on"] = new OpenApiSchema { Type = JsonSchemaType.String, Format = "date", Description = "Дата публикации, если известна." },
                                ["source_url"] = new OpenApiSchema { Type = JsonSchemaType.String, Format = "uri", Description = "URL первоисточника, если известен." }
                            }
                        }
                    }
                }
            };
            return Task.CompletedTask;
        });
        group.MapGet("", async (HttpContext http, AccessService access, AlfaNewsDbContext db, CancellationToken cancellationToken) =>
        {
            var user = SessionAuthenticationHandler.CurrentUser(http.User);
            await access.RequireMaterialsAsync(user, cancellationToken);
            var documents = await db.ResearchDocRows.AsNoTracking().OrderBy(row => row.CreatedAt).ToArrayAsync(cancellationToken);
            var failedIds = documents.Where(row => row.OcrStatus == "failed").Select(row => row.Id).ToArray();
            var failedPages = (await db.ResearchPageRows.AsNoTracking().Where(row => failedIds.Contains(row.DocumentId))
                    .Select(row => new { row.DocumentId, row.OcrStatus, row.LastError }).ToArrayAsync(cancellationToken))
                .ToLookup(row => row.DocumentId, row => (row.OcrStatus, row.LastError));
            return new DocumentListResponse(documents.Select(row => new DocumentSummaryDto(row.Id.ToString(), row.Title,
                row.PublishedOn, row.PageCount, row.Version, Parse<OcrStatus>(row.OcrStatus), Parse<ReviewStatus>(row.ReviewStatus),
                row.CreatedAt, row.UploadedBy == user.UserId, DocumentStatuses.WaitsForKey(row.OcrStatus, failedPages[row.Id].ToArray()))).ToArray());
        });
        group.MapGet("/{id:guid}", async (Guid id, HttpContext http, AccessService access, AlfaNewsDbContext db, CancellationToken cancellationToken) =>
        {
            await access.RequireMaterialsAsync(SessionAuthenticationHandler.CurrentUser(http.User), cancellationToken);
            var document = await db.ResearchDocRows.AsNoTracking().SingleOrDefaultAsync(row => row.Id == id, cancellationToken)
                ?? throw new AccessException(404, "not_found", "Документ не найден.");
            var pages = await db.ResearchPageRows.AsNoTracking().Where(row => row.DocumentId == id).OrderBy(row => row.PageNo).ToArrayAsync(cancellationToken);
            var pageIds = pages.Select(row => row.Id).ToArray();
            var chunks = await db.ResearchChunkRows.AsNoTracking().Where(row => pageIds.Contains(row.PageId)).OrderBy(row => row.LocalId).ToArrayAsync(cancellationToken);
            return new DocumentResponse(id.ToString(), document.Title, document.PublishedOn, document.PageCount, document.Version,
                Parse<OcrStatus>(document.OcrStatus), Parse<ReviewStatus>(document.ReviewStatus), pages.Select(page => new DocumentPageDto(page.PageNo,
                    Parse<OcrStatus>(page.OcrStatus), Parse<ReviewStatus>(page.ReviewStatus), $"/api/research/{id}/pages/{page.PageNo}",
                    chunks.Where(chunk => chunk.PageId == page.Id).Select(chunk => new ReviewableChunkDto(chunk.Id.ToString(), Parse<ResearchBlockKind>(chunk.Kind),
                        chunk.Text, Parse<ReviewStatus>(chunk.ReviewStatus), chunk.TablesJson.Deserialize<TableDraft[]>(JobKinds.JsonOptions)!,
                        chunk.ChartData.Deserialize<ReviewableChartDto[]>(JobKinds.JsonOptions)!)).ToArray(),
                    page.LastError switch
                    {
                        null => null,
                        DocumentStatuses.KeyRequired => "Не задан ключ модели. После настройки ключа повторите задание страницы.",
                        _ => "Не удалось распознать страницу. Требуется повтор задания."
                    })).ToArray(),
                DocumentStatuses.WaitsForKey(document.OcrStatus, pages.Select(page => (page.OcrStatus, page.LastError)).ToArray()));
        });
        group.MapPost("/{id:guid}/pages/{pageNo:int}/review", async (Guid id, int pageNo, PageReviewRequest request, HttpContext http,
            AccessService access, ResearchReview review, CancellationToken cancellationToken) =>
        {
            var user = SessionAuthenticationHandler.CurrentUser(http.User);
            await access.RequireOperatorAsync(user, cancellationToken);
            var ifMatch = http.Request.Headers.IfMatch.ToString();
            var result = await review.ApplyAsync(user, id, pageNo, ifMatch.Length > 0 ? ifMatch : null, request, cancellationToken);
            return Results.Json(result.Response, statusCode: StatusCodes.Status202Accepted);
        }).RequireAuthorization(SessionAuthenticationHandler.OperatorPolicy);
        // D17: новость по ссылке с сайта подключённого источника; статью загружает Worker, дальше обычный разбор.
        app.MapPost("/api/admin/news", async (ManualNewsRequest request, HttpContext http, AccessService access, ManualNewsService news,
            CancellationToken cancellationToken) =>
        {
            var user = SessionAuthenticationHandler.CurrentUser(http.User);
            await access.RequireMaterialsAsync(user, cancellationToken);
            var job = await news.EnqueueAsync(request, user.UserId, cancellationToken);
            return Results.Accepted($"/api/jobs/{job.Id}", new AcceptedResponse(job.Id.ToString(), $"/api/jobs/{job.Id}"));
        }).RequireAuthorization();
        app.MapGet("/api/admin/news/sources", async (HttpContext http, AccessService access, ManualNewsService news,
            CancellationToken cancellationToken) =>
        {
            await access.RequireMaterialsAsync(SessionAuthenticationHandler.CurrentUser(http.User), cancellationToken);
            return await news.SourcesAsync(cancellationToken);
        }).RequireAuthorization();
        app.MapGet("/api/admin/news", async (HttpContext http, AccessService access, ManualNewsService news, CancellationToken cancellationToken) =>
        {
            var user = SessionAuthenticationHandler.CurrentUser(http.User);
            await access.RequireMaterialsAsync(user, cancellationToken);
            return await news.ListAsync(user.UserId, http.TraceIdentifier, cancellationToken);
        }).RequireAuthorization();
        app.MapGet("/api/research/{id:guid}/pages/{pageNo:int}", async (Guid id, int pageNo, HttpContext http, AccessService access,
            AlfaNewsDbContext db, PdfOptions options, PdfTools tools, CancellationToken cancellationToken) =>
        {
            var page = await access.RequireResearchPageAsync(SessionAuthenticationHandler.CurrentUser(http.User), id, pageNo, cancellationToken);
            var path = options.Resolve(page.ImagePath);
            if (!File.Exists(path))
            {
                var document = await db.ResearchDocRows.AsNoTracking().SingleAsync(row => row.Id == id, cancellationToken);
                var source = options.Resolve(document.FilePath);
                if (!File.Exists(source))
                    throw new AccessException(404, "not_found", "Исходный PDF ещё не доступен на устройстве.");
                await tools.RenderAsync(source, pageNo, path, cancellationToken);
            }
            return Results.File(path, "image/png");
        }).RequireAuthorization();
        app.MapPost("/api/admin/jobs/{id:guid}/retry", async (Guid id, HttpContext http, AccessService access, JobQueue queue,
            CancellationToken cancellationToken) =>
        {
            await access.RequireOperatorAsync(SessionAuthenticationHandler.CurrentUser(http.User), cancellationToken);
            var job = await access.RequireJobAsync(SessionAuthenticationHandler.CurrentUser(http.User), id, cancellationToken);
            if (job.Status != "failed")
                throw new AccessException(409, "invalid_input", "Повтор доступен только для ошибочного задания.");
            if (!await queue.RetryAsync(id, cancellationToken))
                throw new AccessException(409, "invalid_input", "Состояние задания изменилось.");
            return Results.Accepted($"/api/jobs/{id}", new AcceptedResponse(id.ToString(), $"/api/jobs/{id}"));
        }).RequireAuthorization(SessionAuthenticationHandler.OperatorPolicy);
    }

    private static T Parse<T>(string value) where T : struct, Enum => Enum.Parse<T>(value.Replace("_", ""), true);
}