import { dotnet } from './_framework/dotnet.js'

const { getAssemblyExports, getConfig } = await dotnet
    .withDiagnosticTracing(false)
    .create();

const config = getConfig();
const exports = await getAssemblyExports(config.mainAssemblyName);

const text = exports.Interop.Version();
console.log(text);
document.getElementById('out').innerText = text;

await dotnet.run();
