using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using AppoMobi.Gestures;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<BlazorWasmSample.App>("#app");

builder.Services.AddBlazorGestures();

await builder.Build().RunAsync();
