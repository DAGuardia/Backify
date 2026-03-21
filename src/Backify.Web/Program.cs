using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Backify.Web;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var apiBase = builder.Configuration["ApiBaseUrl"];
if (string.IsNullOrEmpty(apiBase)) apiBase = builder.HostEnvironment.BaseAddress;
builder.Services.AddScoped(sp => new HttpClient(new CookieHandler { InnerHandler = new HttpClientHandler() })
{
    BaseAddress = new Uri(apiBase),
});

await builder.Build().RunAsync();
