#if MACCATALYST || IOS
using System.Runtime.InteropServices;

namespace chatbot
{
    public static class NativeLibraryHelper
    {
        private static bool _configured = false;
        private static readonly object _lock = new object();
        private static readonly List<nint> _loadedHandles = new List<nint>();

        private const int RTLD_NOW = 0x2;
        private const int RTLD_GLOBAL = 0x8;

        [DllImport("libdl")]
        private static extern nint dlopen(string path, int mode);

        [DllImport("libdl")]
        private static extern IntPtr dlerror();

        public static void ConfigureNativeLibrary()
        {
            if (_configured)
                return;

            lock (_lock)
            {
                if (_configured)
                    return;

                try
                {
                    // No MacCatalyst, os recursos ficam em Contents/Resources dentro do bundle
                    var appPath = AppContext.BaseDirectory;
                    
                    // Se estamos dentro de um bundle, o BaseDirectory pode estar em Contents/MacOS/
                    // Precisamos subir para o bundle root
                    var bundlePath = appPath;
                    if (appPath.Contains(".app/Contents/MacOS"))
                    {
                        bundlePath = Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(appPath)))!;
                    }
                    else if (appPath.Contains(".app/Contents"))
                    {
                        bundlePath = Path.GetDirectoryName(Path.GetDirectoryName(appPath))!;
                    }
                    
                    // Tentar diferentes caminhos possíveis
                    var possiblePaths = new List<string>
                    {
                        Path.Combine(appPath, "runtimes", "osx-arm64", "native"),
                        Path.Combine(appPath, "runtimes", "osx-x64", "native"),
                        Path.Combine(bundlePath, "Contents", "Resources", "runtimes", "osx-arm64", "native"),
                        Path.Combine(bundlePath, "Contents", "Resources", "runtimes", "osx-x64", "native"),
                        Path.Combine(appPath, "Contents", "Resources", "runtimes", "osx-arm64", "native"),
                        Path.Combine(appPath, "Contents", "Resources", "runtimes", "osx-x64", "native"),
                    };

                    // Também procurar recursivamente a partir do BaseDirectory
                    if (Directory.Exists(appPath))
                    {
                        try
                        {
                            var foundRuntimes =
 Directory.GetDirectories(appPath, "runtimes", SearchOption.AllDirectories);
                            foreach (var runtimeDir in foundRuntimes)
                            {
                                var osxArm64 = Path.Combine(runtimeDir, "osx-arm64", "native");
                                var osxX64 = Path.Combine(runtimeDir, "osx-x64", "native");
                                if (Directory.Exists(osxArm64)) possiblePaths.Add(osxArm64);
                                if (Directory.Exists(osxX64)) possiblePaths.Add(osxX64);
                            }
                        }
                        catch { /* Ignore search errors */ }
                    }

                    // Prefer the runtime that matches the current process architecture
                    var arch = RuntimeInformation.ProcessArchitecture;
                    string? libllamaPath = null;
                    string? nativeLibDir = null;

                    IEnumerable<string> orderedPaths = possiblePaths.Distinct();
                    if (arch == Architecture.Arm64)
                    {
                        orderedPaths = orderedPaths
                            .OrderByDescending(p => p.Contains("osx-arm64"))
                            .ThenByDescending(p => p.Contains("osx-x64"));
                    }
                    else if (arch == Architecture.X64)
                    {
                        orderedPaths = orderedPaths
                            .OrderByDescending(p => p.Contains("osx-x64"))
                            .ThenByDescending(p => p.Contains("osx-arm64"));
                    }

                    foreach (var nativeLibPath in orderedPaths)
                    {
                        if (Directory.Exists(nativeLibPath))
                        {
                            var libllama = Path.Combine(nativeLibPath, "libllama.dylib");
                            if (File.Exists(libllama))
                            {
                                libllamaPath = libllama;
                                nativeLibDir = nativeLibPath;
                                break;
                            }
                        }
                    }

                    if (libllamaPath != null && nativeLibDir != null)
                    {
                    // Preload dependencies from the same directory so @rpath can resolve.
                    // Order matters: libggml.dylib depends on libggml-cpu.dylib.
                    var deps = new[]
                    {
                        "libggml-base.dylib",
                        "libggml-cpu.dylib",
                        "libggml-blas.dylib",
                        "libggml-metal.dylib",
                        "libggml.dylib",
                        "libllama.dylib",
                        "libmtmd.dylib",
                    };

                    foreach (var dep in deps)
                    {
                        var depPath = Path.Combine(nativeLibDir, dep);
                        if (File.Exists(depPath))
                        {
                            try
                            {
                                // Use dlopen with RTLD_GLOBAL to expose symbols for dependent dylibs.
                                var handle = dlopen(depPath, RTLD_NOW | RTLD_GLOBAL);
                                if (handle == 0)
                                {
                                    var errPtr = dlerror();
                                    var err = errPtr == IntPtr.Zero ? "unknown" : Marshal.PtrToStringAnsi(errPtr);
                                    throw new Exception(err ?? "unknown");
                                }
                                _loadedHandles.Add(handle);
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"Warning: Could not preload {dep}: {ex.Message}");
                            }
                        }
                    }

                        // Usar o nome completo do tipo para evitar importar o namespace antes da configuração
                        // Primeiro tentar carregar o assembly se ainda não foi carregado
                        System.Reflection.Assembly? llamasharpAssembly = null;
                        try
                        {
                            llamasharpAssembly = System.Reflection.Assembly.Load("LLamaSharp");
                        }
                        catch
                        {
                            // Tentar encontrar o assembly já carregado
                            llamasharpAssembly = System.AppDomain.CurrentDomain.GetAssemblies()
                                .FirstOrDefault(a => a.GetName().Name == "LLamaSharp");
                        }

                        if (llamasharpAssembly != null)
                        {
                            var nativeLibraryConfigType =
 llamasharpAssembly.GetType("LLama.Native.NativeLibraryConfig");
                            if (nativeLibraryConfigType != null)
                            {
                                var llamaProperty = nativeLibraryConfigType.GetProperty("LLama");
                                if (llamaProperty != null)
                                {
                                    var llamaConfig = llamaProperty.GetValue(null);
                                    var withSearchDirectoryMethod =
 llamaConfig?.GetType().GetMethod("WithSearchDirectory", new[] { typeof(string) });
                                    withSearchDirectoryMethod?.Invoke(llamaConfig, new object[] { nativeLibDir });
                                    var withLibraryMethod =
 llamaConfig?.GetType().GetMethod("WithLibrary", new[] { typeof(string) });
                                    withLibraryMethod?.Invoke(llamaConfig, new object[] { libllamaPath });
                                    System.Diagnostics.Debug.WriteLine($"Configured LLama native library: {libllamaPath}");
                                }
                            }
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine($"Warning: Could not load LLamaSharp assembly for configuration");
                        }
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"Warning: Could not find libllama.dylib. Searched in: {string.Join(", ", possiblePaths)}");
                        System.Diagnostics.Debug.WriteLine($"AppContext.BaseDirectory: {appPath}");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Warning: Could not configure native library path: {ex.Message}");
                    System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                }

                _configured = true;
            }
        }
    }
}
#endif