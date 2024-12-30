using cs2_rockthevote.Core;

namespace cs2_rockthevote
{
    public class MapLister : IPluginDependency<Plugin, Config>
    {
        public Map[]? Maps { get; private set; } = null;
        public bool MapsLoaded { get; private set; } = false;

        public event EventHandler<Map[]>? EventMapsLoaded;

        private Plugin? _plugin;

        public MapLister()
        {
        }

        public void Clear()
        {
            MapsLoaded = false;
            Maps = null;
        }

        void LoadMaps()
        {
            // Clear existing data.
            Clear();

            // Grab the raw filename from the ConVar.
            string filenameRaw = _plugin!.RockTheVoteMaplistFile.Value;
            // Forcefully remove any embedded quotes (") in case it's coming in as "maplist_surf_easy.txt" 
            // or has extra quotes for some reason.
            string filename = filenameRaw.Replace("\"", "").Trim();

            // Determine the plugin's folder by removing the filename part of ModulePath.
            string dllFolder = Path.GetDirectoryName(_plugin.ModulePath)
                ?? throw new DirectoryNotFoundException($"Could not find plugin folder for {_plugin.ModulePath}");

            // Combine the folder + filename into a path.
            string filePath = Path.Combine(dllFolder, filename);

            if (!File.Exists(filePath))
                throw new FileNotFoundException($"MapLister: File not found => {filePath}");

            // Read & parse lines into `Map` objects.
            Maps = File.ReadAllText(filePath)
                .Replace("\r\n", "\n")
                .Split("\n")
                .Select(x => x.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x) && !x.StartsWith("//"))
                .Select(mapLine =>
                {
                    var args = mapLine.Split(':');
                    return new Map(args[0], args.Length == 2 ? args[1] : null);
                })
                .ToArray();

            MapsLoaded = true;
            EventMapsLoaded?.Invoke(this, Maps!);
        }

        public void OnMapStart(string _map)
        {
            if (_plugin is not null)
                LoadMaps();
        }

        public void OnLoad(Plugin plugin)
        {
            _plugin = plugin;
            LoadMaps();
        }
    }
}
