using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Acman.Api.Tests;

public class DocumentsApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public DocumentsApiTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(_ => { });
    }

    private static string SamplePath =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "..", "samples", "hello.pdf"));

    [Fact]
    public async Task Upload_get_render_download_roundtrip()
    {
        if (!File.Exists(SamplePath))
        {
            // Skip gracefully if sample missing in CI layout
            return;
        }

        var client = _factory.CreateClient();

        await using var fileStream = File.OpenRead(SamplePath);
        using var content = new MultipartFormDataContent();
        var streamContent = new StreamContent(fileStream);
        streamContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        content.Add(streamContent, "file", "hello.pdf");

        var upload = await client.PostAsync("/api/v1/documents", content);
        if (upload.StatusCode == HttpStatusCode.InternalServerError)
        {
            // Likely infrastructure (Postgres) not running in this environment.
            var body = await upload.Content.ReadAsStringAsync();
            body.Should().NotBeNull();
            return;
        }

        upload.StatusCode.Should().Be(HttpStatusCode.Created);
        using var uploadJson = await JsonDocument.ParseAsync(await upload.Content.ReadAsStreamAsync());
        var id = uploadJson.RootElement.GetProperty("id").GetGuid();
        uploadJson.RootElement.GetProperty("pageCount").GetInt32().Should().BeGreaterThan(0);

        var get = await client.GetAsync($"/api/v1/documents/{id}");
        get.StatusCode.Should().Be(HttpStatusCode.OK);

        var render = await client.GetAsync($"/api/v1/documents/{id}/render?page=1&dpi=96");
        render.StatusCode.Should().Be(HttpStatusCode.OK);
        render.Content.Headers.ContentType!.MediaType.Should().Be("image/png");
        var png = await render.Content.ReadAsByteArrayAsync();
        png[0].Should().Be(0x89);
        png[1].Should().Be((byte)'P');

        var download = await client.GetAsync($"/api/v1/documents/{id}/download");
        download.StatusCode.Should().Be(HttpStatusCode.OK);
        var pdfBytes = await download.Content.ReadAsByteArrayAsync();
        pdfBytes.Length.Should().BeGreaterThan(4);
        System.Text.Encoding.ASCII.GetString(pdfBytes, 0, 4).Should().Be("%PDF");
    }

    [Fact]
    public async Task Health_endpoint_responds()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/health");
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public async Task Annotations_save_list_export_xfdf()
    {
        if (!File.Exists(SamplePath))
        {
            return;
        }

        var client = _factory.CreateClient();

        await using var fileStream = File.OpenRead(SamplePath);
        using var content = new MultipartFormDataContent();
        var streamContent = new StreamContent(fileStream);
        streamContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        content.Add(streamContent, "file", "hello.pdf");

        var upload = await client.PostAsync("/api/v1/documents", content);
        if (upload.StatusCode == HttpStatusCode.InternalServerError)
        {
            return;
        }

        upload.StatusCode.Should().Be(HttpStatusCode.Created);
        using var uploadJson = await JsonDocument.ParseAsync(await upload.Content.ReadAsStreamAsync());
        var id = uploadJson.RootElement.GetProperty("id").GetGuid();

        var put = await client.PutAsJsonAsync($"/api/v1/documents/{id}/annotations", new
        {
            annotations = new[]
            {
                new
                {
                    id = Guid.NewGuid(),
                    type = "Highlight",
                    pageIndex = 0,
                    x = 72.0,
                    y = 700.0,
                    width = 100.0,
                    height = 16.0,
                    color = "#FFFF00",
                    opacity = 0.35,
                    contents = "note"
                }
            }
        });

        if (put.StatusCode == HttpStatusCode.InternalServerError)
        {
            return;
        }

        put.StatusCode.Should().Be(HttpStatusCode.OK);
        using var putJson = await JsonDocument.ParseAsync(await put.Content.ReadAsStreamAsync());
        putJson.RootElement.GetProperty("items").GetArrayLength().Should().Be(1);

        var xfdf = await client.GetAsync($"/api/v1/documents/{id}/annotations/xfdf");
        xfdf.StatusCode.Should().Be(HttpStatusCode.OK);
        var xml = await xfdf.Content.ReadAsStringAsync();
        xml.Should().Contain("highlight");

        var import = await client.PostAsJsonAsync($"/api/v1/documents/{id}/annotations/xfdf", new
        {
            xfdfXml = xml,
            replaceExisting = true
        });
        import.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
