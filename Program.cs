using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.AspNetCore.Server.Kestrel.Core;

var host = new WebHostBuilder()
    .UseKestrel(options =>
    {
        options.AllowSynchronousIO = true;
    })
    .UseStartup<Startup>()
    .Build();

host.Run();

public class Startup : IStartup
{
    public IServiceProvider ConfigureServices(IServiceCollection services)
    {
        services.Configure<IISServerOptions>((options) => options.AllowSynchronousIO = true);
        services.AddResponseCompression(options => {
            // compression for https is generally a potential vulnerability
            // we mitigate using a random length header set in middleware
            options.EnableForHttps = true;
            options.MimeTypes = new[] { "text/html; charset=utf-8", "text/html" };
            options.Providers.Add<BrotliCompressionProvider>();
        });
        return services.BuildServiceProvider();
    }

    void IStartup.Configure(IApplicationBuilder app)
    {
        app.UseDeveloperExceptionPage();
        app.UseResponseCompression();
        app.UseWebSockets();
        app.Use((HttpContext context, Func<Task> next) =>
        {
            const string dummyBreachMitigation = "!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!";
            if (!context.Response.Headers.ContainsKey("X-Cento-Padding")) context.Response.Headers["X-Cento-Padding"] = dummyBreachMitigation;
            return next();
        });
        app.Use((HttpContext context, Func<Task> next) =>
        {
            if (context.Request.Method != "GET" && context.Request.Method != "HEAD")
            {
                context.Response.StatusCode = 405;
                return Task.CompletedTask;
            }
            if (context.Request.Path != "/")
            {
                context.Response.StatusCode = 404;
                return Task.CompletedTask;
            }
            context.Response.ContentType = "text/html; charset=utf-8";
            if (context.Request.Method == "HEAD")
            {
                return Task.CompletedTask;
            }
            return BuildResponseBody(context);
        });
    }

    private byte[]? databytes;

    private async Task BuildResponseBody(HttpContext context)
    {
        databytes ??= System.IO.File.ReadAllBytes("content.dat");
        await context.Response.Body.WriteAsync(databytes.AsMemory(0, 16384));
        await context.Response.Body.WriteAsync(databytes.AsMemory(16384, 116));
    }
}