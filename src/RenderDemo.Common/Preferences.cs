using System;
using System.IO;
using System.Runtime.InteropServices;
using Newtonsoft.Json;
using Veldrid.Graphics;

namespace Veldrid.RenderDemo
{
    public class Preferences
    {
        public bool AllowOpenGLDebugContexts { get; set; } = false;
        public bool AllowDirect3DDebugDevice { get; set; } = false;
        public GraphicsBackend PreferredBackend { get; set; } = GetPlatformDefaultBackend();
        public string MenuFont { get; set; } = string.Empty;
        public float FontSize { get; set; } = 15;

        private static Preferences _instance;
        public static Preferences Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = LoadPreferences();
                }

                return _instance;
            }
        }

        private static Preferences LoadPreferences()
        {
            string preferencesFile = Path.Combine(SpecialFolders.VeldridConfigFolder, "config.json");
            System.Diagnostics.Debug.WriteLine("Loading preferences from " + preferencesFile);
            if (File.Exists(preferencesFile))
            {
                string json = File.ReadAllText(preferencesFile);
                return JsonConvert.DeserializeObject<Preferences>(json);
            }

            else
            {
                var ret = new Preferences();
                string json = JsonConvert.SerializeObject(ret);
                if (!Directory.Exists(SpecialFolders.VeldridConfigFolder))
                {
                    Directory.CreateDirectory(SpecialFolders.VeldridConfigFolder);
                }
                File.WriteAllText(preferencesFile, json);
                return ret;
            }
        }

        internal void Save()
        {
            string preferencesFile = Path.Combine(SpecialFolders.VeldridConfigFolder, "config.json");
            string json = JsonConvert.SerializeObject(this);
            if (!Directory.Exists(SpecialFolders.VeldridConfigFolder))
            {
                Directory.CreateDirectory(SpecialFolders.VeldridConfigFolder);
            }
            File.WriteAllText(preferencesFile, json);
        }

        private static GraphicsBackend GetPlatformDefaultBackend()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return GraphicsBackend.Direct3D11;
            }
            else
            {
                return GraphicsBackend.OpenGL;
            }
        }
    }
}