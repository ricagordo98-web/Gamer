using Gamer.Ui;
using Gamer.Ui.Services;
using Gamer.Web;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

WebAssemblyHostBuilder builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<GamerApp>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddSingleton<IKeyValueStore, LocalStorageKeyValueStore>();
builder.Services.AddGamerUi(builder.HostEnvironment.BaseAddress);

await builder.Build().RunAsync();
