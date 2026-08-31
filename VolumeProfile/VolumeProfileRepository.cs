#region Using declarations
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using NinjaTrader.NinjaScript.Indicators;
#endregion

namespace NinjaTrader.NinjaScript.Indicators.VolumeProfilePro
{
    /// <summary>
    /// Gestionnaire de persistance SQLite pour le Volume Profile avec cache mémoire
    /// instantané et worker d'écriture asynchrone par lots (Zero-Latency Hot-Path).
    /// </summary>
    public sealed class VolumeProfileRepository : IDisposable
    {
        #region Champs & Propriétés

        private readonly string dbPath;
        private readonly Action<string> logAction;
        private readonly object dbLock = new object();

        private DbConnection connection;
        private DbProviderFactory providerFactory;
        private bool isDisposed;
        private bool isInitialized;
        private bool isSqliteAvailable;

        // Cache RAM instantané pour lecture OnBarUpdate (0 ms latence)
        private readonly ConcurrentDictionary<string, ClosedVolumeProfile> profileCacheByKey = new ConcurrentDictionary<string, ClosedVolumeProfile>(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<string, List<ClosedVolumeProfile>> profileCacheBySymbolType = new ConcurrentDictionary<string, List<ClosedVolumeProfile>>(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<long, VolumeProfileZoneState> zoneStateCache = new ConcurrentDictionary<long, VolumeProfileZoneState>();

        // File d'attente d'écriture asynchrone (Non-blocking)
        private readonly ConcurrentQueue<Action<DbConnection>> writeQueue = new ConcurrentQueue<Action<DbConnection>>();
        private readonly AutoResetEvent writeSignal = new AutoResetEvent(false);
        private readonly CancellationTokenSource cts = new CancellationTokenSource();
        private Task backgroundWorkerTask;

        #endregion

        #region Constructeur & Initialisation

        public VolumeProfileRepository(string databasePath, Action<string> logger = null)
        {
            this.dbPath = databasePath;
            this.logAction = logger ?? (msg => { });
        }

        public bool Initialize()
        {
            if (isInitialized) return isSqliteAvailable;

            lock (dbLock)
            {
                if (isInitialized) return isSqliteAvailable;

                try
                {
                    string dir = Path.GetDirectoryName(dbPath);
                    if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    {
                        Directory.CreateDirectory(dir);
                    }

                    // Tentative de résolution du driver SQLite ADO.NET
                    providerFactory = ResolveSqliteProvider();
                    if (providerFactory != null)
                    {
                        connection = providerFactory.CreateConnection();
                        connection.ConnectionString = string.Format("Data Source={0};", dbPath);
                        connection.Open();

                        ConfigurePragmas();
                        CreateTables();

                        isSqliteAvailable = true;
                        logAction(string.Format("VolumeProfile SQLite actif : {0}", dbPath));
                    }
                    else
                    {
                        isSqliteAvailable = false;
                        logAction("VolumeProfile SQLite : Driver ADO.NET non trouvé. Mode cache mémoire actif.");
                    }

                    // Démarrage du worker asynchrone
                    backgroundWorkerTask = Task.Factory.StartNew(ProcessWriteQueue, cts.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
                    isInitialized = true;
                }
                catch (Exception ex)
                {
                    isSqliteAvailable = false;
                    isInitialized = true;
                    logAction(string.Format("VolumeProfile SQLite Init Erreur : {0}", ex.Message));
                }

                return isSqliteAvailable;
            }
        }

        private static DbProviderFactory ResolveSqliteProvider()
        {
            // 1. Recherche dans les types déjà chargés
            Type[] knownTypes = new[]
            {
                Type.GetType("Microsoft.Data.Sqlite.SqliteFactory, Microsoft.Data.Sqlite"),
                Type.GetType("System.Data.SQLite.SQLiteFactory, System.Data.SQLite"),
                Type.GetType("Mono.Data.Sqlite.SqliteFactory, Mono.Data.Sqlite")
            };

            foreach (var t in knownTypes)
            {
                if (t != null)
                {
                    FieldInfo instanceField = t.GetField("Instance", BindingFlags.Public | BindingFlags.Static);
                    if (instanceField != null)
                    {
                        return instanceField.GetValue(null) as DbProviderFactory;
                    }
                    return Activator.CreateInstance(t) as DbProviderFactory;
                }
            }

            // 2. Recherche dans les assemblies chargés du domaine d'application
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                string asmName = asm.GetName().Name;
                if (asmName.IndexOf("Sqlite", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    Type factoryType = asm.GetType("Microsoft.Data.Sqlite.SqliteFactory") ?? asm.GetType("System.Data.SQLite.SQLiteFactory");
                    if (factoryType != null)
                    {
                        FieldInfo instanceField = factoryType.GetField("Instance", BindingFlags.Public | BindingFlags.Static);
                        if (instanceField != null)
                            return instanceField.GetValue(null) as DbProviderFactory;
                        return Activator.CreateInstance(factoryType) as DbProviderFactory;
                    }
                }
            }

            return null;
        }

        private void ConfigurePragmas()
        {
            ExecuteNonQuery("PRAGMA journal_mode = WAL;");
            ExecuteNonQuery("PRAGMA synchronous = NORMAL;");
            ExecuteNonQuery("PRAGMA busy_timeout = 5000;");
            ExecuteNonQuery("PRAGMA foreign_keys = ON;");
        }

        private void CreateTables()
        {
            string ddlProfiles = @"
                CREATE TABLE IF NOT EXISTS vp_profiles (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    symbol TEXT NOT NULL,
                    exchange TEXT,
                    session_template TEXT,
                    profile_type TEXT NOT NULL,
                    period_key TEXT NOT NULL UNIQUE,
                    period_start_utc TEXT NOT NULL,
                    period_end_utc TEXT NOT NULL,
                    vah REAL NOT NULL,
                    poc REAL NOT NULL,
                    val REAL NOT NULL,
                    vwap REAL,
                    vwap_std_dev REAL,
                    vwap_sd1_upper REAL,
                    vwap_sd1_lower REAL,
                    vwap_sd2_upper REAL,
                    vwap_sd2_lower REAL,
                    vwap_sd3_upper REAL,
                    vwap_sd3_lower REAL,
                    total_volume REAL NOT NULL,
                    value_area_percent INTEGER NOT NULL,
                    tick_size REAL NOT NULL,
                    calculation_method TEXT,
                    created_at_utc TEXT NOT NULL
                );
                CREATE INDEX IF NOT EXISTS idx_vp_profiles_lookup ON vp_profiles (symbol, profile_type, period_start_utc);
                CREATE INDEX IF NOT EXISTS idx_vp_profiles_key ON vp_profiles (symbol, profile_type, period_key);
            ";

            string ddlNodes = @"
                CREATE TABLE IF NOT EXISTS vp_nodes (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    profile_id INTEGER NOT NULL,
                    node_type TEXT NOT NULL,
                    zone_low REAL NOT NULL,
                    zone_high REAL NOT NULL,
                    peak_price REAL NOT NULL,
                    relative_volume REAL NOT NULL,
                    prominence REAL NOT NULL,
                    created_at_utc TEXT NOT NULL,
                    FOREIGN KEY (profile_id) REFERENCES vp_profiles(id) ON DELETE CASCADE
                );
                CREATE INDEX IF NOT EXISTS idx_vp_nodes_profile ON vp_nodes (profile_id, node_type);
            ";

            string ddlZoneState = @"
                CREATE TABLE IF NOT EXISTS vp_zone_state (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    profile_id INTEGER NOT NULL,
                    node_id INTEGER,
                    level_type TEXT NOT NULL,
                    level_price_low REAL NOT NULL,
                    level_price_high REAL NOT NULL,
                    peak_price REAL NOT NULL,
                    first_touch_utc TEXT,
                    last_touch_utc TEXT,
                    touch_count INTEGER DEFAULT 0,
                    rejection_count INTEGER DEFAULT 0,
                    acceptance_count INTEGER DEFAULT 0,
                    break_count INTEGER DEFAULT 0,
                    state TEXT DEFAULT 'UNTOUCHED',
                    strength_score REAL DEFAULT 100.0,
                    last_reaction TEXT DEFAULT 'NONE',
                    active INTEGER DEFAULT 1,
                    updated_at_utc TEXT NOT NULL
                );
                CREATE INDEX IF NOT EXISTS idx_vp_zone_state_active ON vp_zone_state (profile_id, active);
            ";

            string ddlSwingTrades = @"
                CREATE TABLE IF NOT EXISTS swing_trades (
                    trade_id TEXT PRIMARY KEY,
                    signal_id TEXT,
                    symbol TEXT NOT NULL,
                    direction INTEGER NOT NULL,
                    setup_type INTEGER NOT NULL,
                    tier INTEGER NOT NULL,
                    status TEXT NOT NULL,
                    entry_time_utc TEXT NOT NULL,
                    exit_time_utc TEXT,
                    entry_price REAL NOT NULL,
                    exit_price REAL,
                    initial_stop REAL NOT NULL,
                    current_stop REAL NOT NULL,
                    target1_price REAL NOT NULL,
                    target2_price REAL NOT NULL,
                    initial_contracts INTEGER NOT NULL,
                    remaining_contracts INTEGER NOT NULL,
                    tp1_hit INTEGER NOT NULL,
                    realized_r REAL NOT NULL,
                    realized_usd REAL NOT NULL,
                    exit_reason TEXT,
                    notes TEXT,
                    last_update_utc TEXT NOT NULL
                );
                CREATE INDEX IF NOT EXISTS idx_swing_trades_status ON swing_trades (symbol, status);
            ";

            ExecuteNonQuery(ddlProfiles);
            ExecuteNonQuery(ddlNodes);
            ExecuteNonQuery(ddlZoneState);
            ExecuteNonQuery(ddlSwingTrades);

            // Migration automatique douce pour les bases de données existantes
            MigrateSchemaIfNeeded();
        }

        private void MigrateSchemaIfNeeded()
        {
            string[] migrationColumns = new[]
            {
                "ALTER TABLE vp_profiles ADD COLUMN vwap REAL;",
                "ALTER TABLE vp_profiles ADD COLUMN vwap_std_dev REAL;",
                "ALTER TABLE vp_profiles ADD COLUMN vwap_sd1_upper REAL;",
                "ALTER TABLE vp_profiles ADD COLUMN vwap_sd1_lower REAL;",
                "ALTER TABLE vp_profiles ADD COLUMN vwap_sd2_upper REAL;",
                "ALTER TABLE vp_profiles ADD COLUMN vwap_sd2_lower REAL;",
                "ALTER TABLE vp_profiles ADD COLUMN vwap_sd3_upper REAL;",
                "ALTER TABLE vp_profiles ADD COLUMN vwap_sd3_lower REAL;"
            };

            foreach (var sql in migrationColumns)
            {
                try { ExecuteNonQuery(sql); } catch { /* Ignore si déjà existant */ }
            }
        }

        private void ExecuteNonQuery(string sql)
        {
            if (connection == null || connection.State != ConnectionState.Open) return;
            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = sql;
                cmd.ExecuteNonQuery();
            }
        }

        #endregion

        #region Opérations CRUD & Cache

        /// <summary>
        /// Enregistre ou met à jour un ClosedVolumeProfile dans le cache et en base SQLite.
        /// </summary>
        public void UpsertProfile(ClosedVolumeProfile profile)
        {
            if (profile == null || string.IsNullOrEmpty(profile.PeriodKey)) return;

            // 1. Mise à jour immédiate du cache RAM (0ms)
            profileCacheByKey[profile.PeriodKey] = profile;

            string groupKey = BuildGroupKey(profile.Symbol, profile.ProfileType);
            profileCacheBySymbolType.AddOrUpdate(groupKey,
                k => new List<ClosedVolumeProfile> { profile },
                (k, list) =>
                {
                    lock (list)
                    {
                        int idx = list.FindIndex(p => string.Equals(p.PeriodKey, profile.PeriodKey, StringComparison.OrdinalIgnoreCase));
                        if (idx >= 0) list[idx] = profile;
                        else list.Add(profile);
                        list.Sort((a, b) => b.PeriodStartUtc.CompareTo(a.PeriodStartUtc)); // Plus récent en premier
                    }
                    return list;
                });

            // 2. Enregistrement asynchrone SQLite
            if (!isSqliteAvailable) return;

            EnqueueWrite(conn =>
            {
                using (var tx = conn.BeginTransaction())
                {
                    try
                    {
                        long profileId = 0;

                        // Vérifier si le profil existe déjà
                        using (var cmd = conn.CreateCommand())
                        {
                            cmd.Transaction = tx;
                            cmd.CommandText = "SELECT id FROM vp_profiles WHERE period_key = @key;";
                            AddParam(cmd, "@key", profile.PeriodKey);
                            object result = cmd.ExecuteScalar();
                            if (result != null && result != DBNull.Value)
                            {
                                profileId = Convert.ToInt64(result);
                            }
                        }

                        if (profileId > 0)
                        {
                            // Update
                            using (var cmd = conn.CreateCommand())
                            {
                                cmd.Transaction = tx;
                                cmd.CommandText = @"
                                    UPDATE vp_profiles SET
                                        vah = @vah, poc = @poc, val = @val,
                                        vwap = @vwap, vwap_std_dev = @vwap_sd,
                                        vwap_sd1_upper = @sd1u, vwap_sd1_lower = @sd1l,
                                        vwap_sd2_upper = @sd2u, vwap_sd2_lower = @sd2l,
                                        vwap_sd3_upper = @sd3u, vwap_sd3_lower = @sd3l,
                                        total_volume = @total_volume,
                                        value_area_percent = @va_pct,
                                        tick_size = @tick_size,
                                        created_at_utc = @created
                                    WHERE id = @id;
                                ";
                                AddParam(cmd, "@vah", profile.Vah);
                                AddParam(cmd, "@poc", profile.Poc);
                                AddParam(cmd, "@val", profile.Val);
                                AddParam(cmd, "@vwap", profile.Vwap);
                                AddParam(cmd, "@vwap_sd", profile.VwapStdDev);
                                AddParam(cmd, "@sd1u", profile.VwapSd1Upper);
                                AddParam(cmd, "@sd1l", profile.VwapSd1Lower);
                                AddParam(cmd, "@sd2u", profile.VwapSd2Upper);
                                AddParam(cmd, "@sd2l", profile.VwapSd2Lower);
                                AddParam(cmd, "@sd3u", profile.VwapSd3Upper);
                                AddParam(cmd, "@sd3l", profile.VwapSd3Lower);
                                AddParam(cmd, "@total_volume", profile.TotalVolume);
                                AddParam(cmd, "@va_pct", profile.ValueAreaPercent);
                                AddParam(cmd, "@tick_size", profile.TickSize);
                                AddParam(cmd, "@created", profile.CreatedAtUtc.ToString("o"));
                                AddParam(cmd, "@id", profileId);
                                cmd.ExecuteNonQuery();
                            }

                            // Supprimer anciens nodes pour remplacer
                            using (var cmd = conn.CreateCommand())
                            {
                                cmd.Transaction = tx;
                                cmd.CommandText = "DELETE FROM vp_nodes WHERE profile_id = @id;";
                                AddParam(cmd, "@id", profileId);
                                cmd.ExecuteNonQuery();
                            }
                        }
                        else
                        {
                            // Insert
                            using (var cmd = conn.CreateCommand())
                            {
                                cmd.Transaction = tx;
                                cmd.CommandText = @"
                                    INSERT INTO vp_profiles (
                                        symbol, exchange, session_template, profile_type,
                                        period_key, period_start_utc, period_end_utc,
                                        vah, poc, val,
                                        vwap, vwap_std_dev,
                                        vwap_sd1_upper, vwap_sd1_lower,
                                        vwap_sd2_upper, vwap_sd2_lower,
                                        vwap_sd3_upper, vwap_sd3_lower,
                                        total_volume, value_area_percent,
                                        tick_size, calculation_method, created_at_utc
                                    ) VALUES (
                                        @symbol, @exchange, @session, @type,
                                        @key, @start, @end,
                                        @vah, @poc, @val,
                                        @vwap, @vwap_sd,
                                        @sd1u, @sd1l,
                                        @sd2u, @sd2l,
                                        @sd3u, @sd3l,
                                        @volume, @va_pct,
                                        @tick_size, @method, @created
                                    );
                                ";
                                AddParam(cmd, "@symbol", profile.Symbol);
                                AddParam(cmd, "@exchange", profile.Exchange);
                                AddParam(cmd, "@session", profile.SessionTemplate);
                                AddParam(cmd, "@type", profile.ProfileType.ToString().ToUpperInvariant());
                                AddParam(cmd, "@key", profile.PeriodKey);
                                AddParam(cmd, "@start", profile.PeriodStartUtc.ToString("o"));
                                AddParam(cmd, "@end", profile.PeriodEndUtc.ToString("o"));
                                AddParam(cmd, "@vah", profile.Vah);
                                AddParam(cmd, "@poc", profile.Poc);
                                AddParam(cmd, "@val", profile.Val);
                                AddParam(cmd, "@vwap", profile.Vwap);
                                AddParam(cmd, "@vwap_sd", profile.VwapStdDev);
                                AddParam(cmd, "@sd1u", profile.VwapSd1Upper);
                                AddParam(cmd, "@sd1l", profile.VwapSd1Lower);
                                AddParam(cmd, "@sd2u", profile.VwapSd2Upper);
                                AddParam(cmd, "@sd2l", profile.VwapSd2Lower);
                                AddParam(cmd, "@sd3u", profile.VwapSd3Upper);
                                AddParam(cmd, "@sd3l", profile.VwapSd3Lower);
                                AddParam(cmd, "@volume", profile.TotalVolume);
                                AddParam(cmd, "@va_pct", profile.ValueAreaPercent);
                                AddParam(cmd, "@tick_size", profile.TickSize);
                                AddParam(cmd, "@method", profile.CalculationMethod);
                                AddParam(cmd, "@created", profile.CreatedAtUtc.ToString("o"));
                                cmd.ExecuteNonQuery();
                            }

                            // Récupérer le nouvel ID
                            using (var cmd = conn.CreateCommand())
                            {
                                cmd.Transaction = tx;
                                cmd.CommandText = "SELECT last_insert_rowid();";
                                profileId = Convert.ToInt64(cmd.ExecuteScalar());
                            }
                        }

                        profile.Id = profileId;

                        // Insertion des nodes
                        if (profile.Nodes != null && profile.Nodes.Count > 0)
                        {
                            foreach (var node in profile.Nodes)
                            {
                                node.ProfileId = profileId;
                                using (var cmd = conn.CreateCommand())
                                {
                                    cmd.Transaction = tx;
                                    cmd.CommandText = @"
                                        INSERT INTO vp_nodes (
                                            profile_id, node_type, zone_low, zone_high,
                                            peak_price, relative_volume, prominence, created_at_utc
                                        ) VALUES (
                                            @profile_id, @type, @low, @high,
                                            @peak, @rel_vol, @prominence, @created
                                        );
                                    ";
                                    AddParam(cmd, "@profile_id", profileId);
                                    AddParam(cmd, "@type", node.NodeType.ToString().ToUpperInvariant());
                                    AddParam(cmd, "@low", node.ZoneLow);
                                    AddParam(cmd, "@high", node.ZoneHigh);
                                    AddParam(cmd, "@peak", node.PeakPrice);
                                    AddParam(cmd, "@rel_vol", node.RelativeVolume);
                                    AddParam(cmd, "@prominence", node.Prominence);
                                    AddParam(cmd, "@created", node.CreatedAtUtc.ToString("o"));
                                    cmd.ExecuteNonQuery();
                                }
                            }
                        }

                        tx.Commit();
                    }
                    catch (Exception ex)
                    {
                        tx.Rollback();
                        logAction("SQLite UpsertProfile Erreur : " + ex.Message);
                    }
                }
            });
        }

        /// <summary>
        /// Charge un ClosedVolumeProfile depuis le cache RAM ou SQLite par clé.
        /// </summary>
        public ClosedVolumeProfile GetProfileByKey(string periodKey)
        {
            if (string.IsNullOrEmpty(periodKey)) return null;

            ClosedVolumeProfile p;
            if (profileCacheByKey.TryGetValue(periodKey, out p))
                return p;

            if (!isSqliteAvailable) return null;

            // Lecture synchrone depuis SQLite si absent du cache
            lock (dbLock)
            {
                try
                {
                    using (var cmd = connection.CreateCommand())
                    {
                        cmd.CommandText = "SELECT * FROM vp_profiles WHERE period_key = @key;";
                        AddParam(cmd, "@key", periodKey);
                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                p = ReadProfileFromReader(reader);
                            }
                        }
                    }

                    if (p != null)
                    {
                        p.Nodes = LoadNodesForProfile(p.Id);
                        profileCacheByKey[p.PeriodKey] = p;
                    }
                }
                catch (Exception ex)
                {
                    logAction("GetProfileByKey SQLite Erreur : " + ex.Message);
                }
            }

            return p;
        }

        /// <summary>
        /// Renvoie le dernier profil entièrement clôturé d'un symbole et type de période AVANT la date spécifiée.
        /// Garantit formellement l'absence de Look-Ahead bias.
        /// </summary>
        public ClosedVolumeProfile GetLatestClosedProfile(string symbol, VolumeProfilePeriodType periodType, DateTime beforeUtc)
        {
            string groupKey = BuildGroupKey(symbol, periodType);

            List<ClosedVolumeProfile> list;
            if (profileCacheBySymbolType.TryGetValue(groupKey, out list))
            {
                lock (list)
                {
                    for (int i = 0; i < list.Count; i++)
                    {
                        var p = list[i];
                        if (p.PeriodEndUtc <= beforeUtc && p.Valid)
                            return p;
                    }
                }
            }

            if (!isSqliteAvailable) return null;

            ClosedVolumeProfile result = null;
            lock (dbLock)
            {
                try
                {
                    using (var cmd = connection.CreateCommand())
                    {
                        cmd.CommandText = @"
                            SELECT * FROM vp_profiles
                            WHERE symbol = @symbol
                              AND profile_type = @type
                              AND period_end_utc <= @before
                            ORDER BY period_end_utc DESC
                            LIMIT 1;
                        ";
                        AddParam(cmd, "@symbol", symbol);
                        AddParam(cmd, "@type", periodType.ToString().ToUpperInvariant());
                        AddParam(cmd, "@before", beforeUtc.ToString("o"));

                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                result = ReadProfileFromReader(reader);
                            }
                        }
                    }

                    if (result != null)
                    {
                        result.Nodes = LoadNodesForProfile(result.Id);
                        UpsertProfile(result); // Alimenter le cache
                    }
                }
                catch (Exception ex)
                {
                    logAction("GetLatestClosedProfile SQLite Erreur : " + ex.Message);
                }
            }

            return result;
        }

        /// <summary>
        /// Renvoie les N derniers profils Daily entièrement clôturés d'un symbole AVANT la date spécifiée.
        /// Triés du plus récent au plus ancien. Garantit l'absence de Look-Ahead bias.
        /// Utilisé par le PocMigrationAnalyzer pour détecter la migration directionnelle du POC.
        /// </summary>
        public List<ClosedVolumeProfile> QueryRecentDailyProfiles(string symbol, DateTime beforeUtc, int count)
        {
            var results = new List<ClosedVolumeProfile>();
            if (string.IsNullOrEmpty(symbol) || count <= 0) return results;

            // 1. Tentative depuis le cache RAM
            string groupKey = BuildGroupKey(symbol, VolumeProfilePeriodType.Daily);
            List<ClosedVolumeProfile> list;
            if (profileCacheBySymbolType.TryGetValue(groupKey, out list))
            {
                lock (list)
                {
                    for (int i = 0; i < list.Count && results.Count < count; i++)
                    {
                        var p = list[i];
                        if (p.PeriodEndUtc <= beforeUtc && p.Valid)
                            results.Add(p);
                    }
                }
            }

            if (results.Count >= count) return results;

            // 2. Fallback SQLite si cache insuffisant
            if (!isSqliteAvailable) return results;

            results.Clear();
            lock (dbLock)
            {
                try
                {
                    using (var cmd = connection.CreateCommand())
                    {
                        cmd.CommandText = @"
                            SELECT * FROM vp_profiles
                            WHERE symbol = @symbol
                              AND profile_type = 'DAILY'
                              AND period_end_utc <= @before
                            ORDER BY period_end_utc DESC
                            LIMIT @count;
                        ";
                        AddParam(cmd, "@symbol", symbol);
                        AddParam(cmd, "@before", beforeUtc.ToString("o"));
                        AddParam(cmd, "@count", count);

                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                var p = ReadProfileFromReader(reader);
                                if (p != null) results.Add(p);
                            }
                        }
                    }

                    // Charger les nodes pour chaque profil
                    for (int i = 0; i < results.Count; i++)
                    {
                        results[i].Nodes = LoadNodesForProfile(results[i].Id);
                    }
                }
                catch (Exception ex)
                {
                    logAction("QueryRecentDailyProfiles SQLite Erreur : " + ex.Message);
                }
            }

            return results;
        }

        private List<VolumeProfileNode> LoadNodesForProfile(long profileId)
        {
            var nodes = new List<VolumeProfileNode>();
            if (!isSqliteAvailable || profileId <= 0) return nodes;

            try
            {
                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = "SELECT * FROM vp_nodes WHERE profile_id = @id ORDER BY peak_price ASC;";
                    AddParam(cmd, "@id", profileId);
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var n = new VolumeProfileNode
                            {
                                Id = Convert.ToInt64(reader["id"]),
                                ProfileId = profileId,
                                NodeType = string.Equals(reader["node_type"].ToString(), "HVN", StringComparison.OrdinalIgnoreCase) ? VolumeProfileNodeType.HVN : VolumeProfileNodeType.LVN,
                                ZoneLow = Convert.ToDouble(reader["zone_low"]),
                                ZoneHigh = Convert.ToDouble(reader["zone_high"]),
                                PeakPrice = Convert.ToDouble(reader["peak_price"]),
                                RelativeVolume = Convert.ToDouble(reader["relative_volume"]),
                                Prominence = Convert.ToDouble(reader["prominence"]),
                                CreatedAtUtc = DateTime.Parse(reader["created_at_utc"].ToString(), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)
                            };
                            nodes.Add(n);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                logAction("LoadNodesForProfile Erreur : " + ex.Message);
            }

            return nodes;
        }

        /// <summary>
        /// Met à jour l'état dynamique d'une zone (Touch, Rejection, Acceptance) en RAM et SQLite.
        /// </summary>
        public void UpdateZoneState(VolumeProfileZoneState state)
        {
            if (state == null) return;
            zoneStateCache[state.Id] = state;

            if (!isSqliteAvailable) return;

            EnqueueWrite(conn =>
            {
                try
                {
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = @"
                            INSERT INTO vp_zone_state (
                                id, profile_id, node_id, level_type, level_price_low, level_price_high, peak_price,
                                first_touch_utc, last_touch_utc, touch_count, rejection_count, acceptance_count, break_count,
                                state, strength_score, last_reaction, active, updated_at_utc
                            ) VALUES (
                                @id, @profile_id, @node_id, @level_type, @low, @high, @peak,
                                @first_touch, @last_touch, @touch_count, @rej_count, @acc_count, @break_count,
                                @state, @strength, @reaction, @active, @updated
                            )
                            ON CONFLICT(id) DO UPDATE SET
                                last_touch_utc = @last_touch,
                                touch_count = @touch_count,
                                rejection_count = @rej_count,
                                acceptance_count = @acc_count,
                                break_count = @break_count,
                                state = @state,
                                strength_score = @strength,
                                last_reaction = @reaction,
                                active = @active,
                                updated_at_utc = @updated;
                        ";

                        AddParam(cmd, "@id", state.Id > 0 ? (object)state.Id : DBNull.Value);
                        AddParam(cmd, "@profile_id", state.ProfileId);
                        AddParam(cmd, "@node_id", state.NodeId.HasValue ? (object)state.NodeId.Value : DBNull.Value);
                        AddParam(cmd, "@level_type", state.LevelType ?? "");
                        AddParam(cmd, "@low", state.LevelPriceLow);
                        AddParam(cmd, "@high", state.LevelPriceHigh);
                        AddParam(cmd, "@peak", state.PeakPrice);
                        AddParam(cmd, "@first_touch", state.FirstTouchUtc.HasValue ? (object)state.FirstTouchUtc.Value.ToString("o") : DBNull.Value);
                        AddParam(cmd, "@last_touch", state.LastTouchUtc.HasValue ? (object)state.LastTouchUtc.Value.ToString("o") : DBNull.Value);
                        AddParam(cmd, "@touch_count", state.TouchCount);
                        AddParam(cmd, "@rej_count", state.RejectionCount);
                        AddParam(cmd, "@acc_count", state.AcceptanceCount);
                        AddParam(cmd, "@break_count", state.BreakCount);
                        AddParam(cmd, "@state", state.State.ToString());
                        AddParam(cmd, "@strength", state.StrengthScore);
                        AddParam(cmd, "@reaction", state.LastReaction ?? "");
                        AddParam(cmd, "@active", state.Active ? 1 : 0);
                        AddParam(cmd, "@updated", DateTime.UtcNow.ToString("o"));

                        cmd.ExecuteNonQuery();
                    }
                }
                catch (Exception ex)
                {
                    logAction("UpdateZoneState SQLite Erreur : " + ex.Message);
                }
            });
        }

        #endregion

        #region Worker Asynchrone (Zero-Latency Queue)

        private void EnqueueWrite(Action<DbConnection> writeAction)
        {
            if (isDisposed) return;
            writeQueue.Enqueue(writeAction);
            writeSignal.Set();
        }

        private void ProcessWriteQueue()
        {
            while (!cts.IsCancellationRequested)
            {
                try
                {
                    writeSignal.WaitOne(500);

                    if (connection != null && connection.State == ConnectionState.Open)
                    {
                        int batchCount = 0;
                        Action<DbConnection> action;
                        while (writeQueue.TryDequeue(out action) && batchCount++ < 100)
                        {
                            try
                            {
                                lock (dbLock)
                                {
                                    action(connection);
                                }
                            }
                            catch (Exception ex)
                            {
                                logAction("WriteQueue Erreur Batch : " + ex.Message);
                            }
                        }
                    }
                }
                catch (ThreadAbortException) { break; }
                catch (Exception ex)
                {
                    logAction("ProcessWriteQueue Erreur Globale : " + ex.Message);
                }
            }
        }

        #endregion

        #region Helpers & Sérialisation

        private static string BuildGroupKey(string symbol, VolumeProfilePeriodType periodType)
        {
            return string.Format("{0}_{1}", symbol ?? "SYM", periodType);
        }

        private static void AddParam(DbCommand cmd, string name, object value)
        {
            var p = cmd.CreateParameter();
            p.ParameterName = name;
            p.Value = value ?? DBNull.Value;
            cmd.Parameters.Add(p);
        }

        private static bool HasColumn(DbDataReader reader, string columnName)
        {
            for (int i = 0; i < reader.FieldCount; i++)
            {
                if (string.Equals(reader.GetName(i), columnName, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private static double ReadDoubleSafe(DbDataReader reader, string columnName, double defaultValue = 0.0)
        {
            if (!HasColumn(reader, columnName)) return defaultValue;
            object val = reader[columnName];
            if (val == null || val == DBNull.Value) return defaultValue;
            try { return Convert.ToDouble(val); } catch { return defaultValue; }
        }

        private static ClosedVolumeProfile ReadProfileFromReader(DbDataReader reader)
        {
            return new ClosedVolumeProfile
            {
                Id = Convert.ToInt64(reader["id"]),
                Symbol = reader["symbol"].ToString(),
                Exchange = reader["exchange"].ToString(),
                SessionTemplate = reader["session_template"].ToString(),
                ProfileType = (VolumeProfilePeriodType)Enum.Parse(typeof(VolumeProfilePeriodType), reader["profile_type"].ToString(), true),
                PeriodKey = reader["period_key"].ToString(),
                PeriodStartUtc = DateTime.Parse(reader["period_start_utc"].ToString(), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
                PeriodEndUtc = DateTime.Parse(reader["period_end_utc"].ToString(), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
                Vah = Convert.ToDouble(reader["vah"]),
                Poc = Convert.ToDouble(reader["poc"]),
                Val = Convert.ToDouble(reader["val"]),
                Vwap = ReadDoubleSafe(reader, "vwap", 0.0),
                VwapStdDev = ReadDoubleSafe(reader, "vwap_std_dev", 0.0),
                VwapSd1Upper = ReadDoubleSafe(reader, "vwap_sd1_upper", 0.0),
                VwapSd1Lower = ReadDoubleSafe(reader, "vwap_sd1_lower", 0.0),
                VwapSd2Upper = ReadDoubleSafe(reader, "vwap_sd2_upper", 0.0),
                VwapSd2Lower = ReadDoubleSafe(reader, "vwap_sd2_lower", 0.0),
                VwapSd3Upper = ReadDoubleSafe(reader, "vwap_sd3_upper", 0.0),
                VwapSd3Lower = ReadDoubleSafe(reader, "vwap_sd3_lower", 0.0),
                TotalVolume = Convert.ToDouble(reader["total_volume"]),
                ValueAreaPercent = Convert.ToInt32(reader["value_area_percent"]),
                TickSize = Convert.ToDouble(reader["tick_size"]),
                CalculationMethod = reader["calculation_method"].ToString(),
                CreatedAtUtc = DateTime.Parse(reader["created_at_utc"].ToString(), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
                Valid = true,
                Nodes = new List<VolumeProfileNode>()
            };
        }

        #endregion

        #region Persistance Swing Trades SQLite

        /// <summary>
        /// Sauvegarde ou met à jour l'état complet d'un trade Swing en base SQLite.
        /// </summary>
        public void UpsertSwingTrade(TrackedSwingTrade t)
        {
            if (t == null || string.IsNullOrEmpty(t.TradeId)) return;
            if (!isSqliteAvailable) return;

            EnqueueWrite(conn =>
            {
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
                        INSERT INTO swing_trades (
                            trade_id, signal_id, symbol, direction, setup_type, tier, status,
                            entry_time_utc, exit_time_utc, entry_price, exit_price,
                            initial_stop, current_stop, target1_price, target2_price,
                            initial_contracts, remaining_contracts, tp1_hit,
                            realized_r, realized_usd, exit_reason, notes, last_update_utc
                        ) VALUES (
                            @id, @sig_id, @symbol, @dir, @setup, @tier, @status,
                            @entry_time, @exit_time, @entry_price, @exit_price,
                            @init_stop, @curr_stop, @tp1, @tp2,
                            @init_c, @rem_c, @tp1_hit,
                            @real_r, @real_usd, @reason, @notes, @updated
                        )
                        ON CONFLICT(trade_id) DO UPDATE SET
                            status = excluded.status,
                            exit_time_utc = excluded.exit_time_utc,
                            exit_price = excluded.exit_price,
                            current_stop = excluded.current_stop,
                            remaining_contracts = excluded.remaining_contracts,
                            tp1_hit = excluded.tp1_hit,
                            realized_r = excluded.realized_r,
                            realized_usd = excluded.realized_usd,
                            exit_reason = excluded.exit_reason,
                            notes = excluded.notes,
                            last_update_utc = excluded.last_update_utc;
                    ";
                    AddParam(cmd, "@id", t.TradeId);
                    AddParam(cmd, "@sig_id", t.Signal != null ? t.Signal.Id : string.Empty);
                    AddParam(cmd, "@symbol", t.Signal != null ? t.Signal.Symbol : "UNKNOWN");
                    AddParam(cmd, "@dir", t.IsLong ? 1 : -1);
                    AddParam(cmd, "@setup", t.Signal != null ? (int)t.Signal.SetupType : 0);
                    AddParam(cmd, "@tier", t.Signal != null ? (int)t.Signal.Tier : 0);
                    AddParam(cmd, "@status", t.Closed ? "CLOSED" : "OPEN");
                    AddParam(cmd, "@entry_time", t.EntryTimeUtc.ToString("o"));
                    AddParam(cmd, "@exit_time", t.Closed ? t.ExitTimeUtc.ToString("o") : null);
                    AddParam(cmd, "@entry_price", t.EntryPrice);
                    AddParam(cmd, "@exit_price", t.Closed ? (object)t.ExitPrice : DBNull.Value);
                    AddParam(cmd, "@init_stop", t.InitialStopPrice);
                    AddParam(cmd, "@curr_stop", t.CurrentStopPrice);
                    AddParam(cmd, "@tp1", t.Target1Price);
                    AddParam(cmd, "@tp2", t.Target2Price);
                    AddParam(cmd, "@init_c", t.InitialContracts);
                    AddParam(cmd, "@rem_c", t.RemainingContracts);
                    AddParam(cmd, "@tp1_hit", t.Tp1Hit ? 1 : 0);
                    AddParam(cmd, "@real_r", t.RealizedR);
                    AddParam(cmd, "@real_usd", t.RealizedPnlCurrency);
                    AddParam(cmd, "@reason", t.ExitReason ?? "ACTIVE");
                    AddParam(cmd, "@notes", t.ExecutionNotes ?? string.Empty);
                    AddParam(cmd, "@updated", DateTime.UtcNow.ToString("o"));

                    cmd.ExecuteNonQuery();
                }
            });
        }

        /// <summary>
        /// Charge toutes les positions Swing actives (status = 'OPEN') depuis SQLite.
        /// Permet la reprise transparente overnight après redémarrage ou reconnexion.
        /// </summary>
        public List<TrackedSwingTrade> LoadActiveSwingTrades(string symbol)
        {
            var list = new List<TrackedSwingTrade>();
            if (!isSqliteAvailable) return list;

            lock (dbLock)
            {
                try
                {
                    using (var cmd = connection.CreateCommand())
                    {
                        cmd.CommandText = @"
                            SELECT * FROM swing_trades
                            WHERE symbol = @symbol AND status = 'OPEN';
                        ";
                        AddParam(cmd, "@symbol", symbol);

                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                var t = new TrackedSwingTrade();
                                t.TradeId = reader["trade_id"].ToString();
                                t.IsLong = Convert.ToInt32(reader["direction"]) == 1;
                                t.EntryPrice = Convert.ToDouble(reader["entry_price"], CultureInfo.InvariantCulture);
                                t.InitialStopPrice = Convert.ToDouble(reader["initial_stop"], CultureInfo.InvariantCulture);
                                t.CurrentStopPrice = Convert.ToDouble(reader["current_stop"], CultureInfo.InvariantCulture);
                                t.Target1Price = Convert.ToDouble(reader["target1_price"], CultureInfo.InvariantCulture);
                                t.Target2Price = Convert.ToDouble(reader["target2_price"], CultureInfo.InvariantCulture);
                                t.InitialContracts = Convert.ToInt32(reader["initial_contracts"]);
                                t.RemainingContracts = Convert.ToInt32(reader["remaining_contracts"]);
                                t.PositionSizeContracts = t.RemainingContracts;
                                t.Tp1Hit = Convert.ToInt32(reader["tp1_hit"]) == 1;
                                t.RealizedR = Convert.ToDouble(reader["realized_r"], CultureInfo.InvariantCulture);
                                t.RealizedPnlCurrency = Convert.ToDouble(reader["realized_usd"], CultureInfo.InvariantCulture);
                                t.ExitReason = reader["exit_reason"].ToString();
                                t.ExecutionNotes = reader["notes"].ToString();
                                t.Closed = false;

                                string entryStr = reader["entry_time_utc"].ToString();
                                DateTime entryDt;
                                if (DateTime.TryParse(entryStr, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out entryDt))
                                    t.EntryTimeUtc = entryDt;
                                else
                                    t.EntryTimeUtc = DateTime.UtcNow;

                                t.Signal = new SwingSignal
                                {
                                    Id = reader["signal_id"].ToString(),
                                    Symbol = reader["symbol"].ToString(),
                                    Direction = t.IsLong ? SwingDirection.Long : SwingDirection.Short,
                                    SetupType = (SwingSetupType)Convert.ToInt32(reader["setup_type"]),
                                    Tier = (SwingTier)Convert.ToInt32(reader["tier"]),
                                    EntryPrice = t.EntryPrice,
                                    InitialStopPrice = t.InitialStopPrice,
                                    Target1Price = t.Target1Price,
                                    Target2Price = t.Target2Price,
                                    PositionSizeContracts = t.InitialContracts,
                                    GeneratedTimeUtc = t.EntryTimeUtc
                                };

                                list.Add(t);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    logAction("LoadActiveSwingTrades SQLite Erreur : " + ex.Message);
                }
            }
            return list;
        }

        /// <summary>
        /// Vide de manière synchrone les écritures en attente vers SQLite.
        /// </summary>
        public void FlushQueue()
        {
            if (connection != null && connection.State == ConnectionState.Open)
            {
                lock (dbLock)
                {
                    Action<DbConnection> action;
                    while (writeQueue.TryDequeue(out action))
                    {
                        try { action(connection); } catch { }
                    }
                }
            }
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            if (isDisposed) return;
            isDisposed = true;

            try
            {
                cts.Cancel();
                writeSignal.Set();

                // Vidage final de la file d'attente
                FlushQueue();

                if (connection != null)
                {
                    if (connection.State == ConnectionState.Open)
                        connection.Close();
                    connection.Dispose();
                    connection = null;
                }

                cts.Dispose();
                writeSignal.Dispose();
            }
            catch (Exception ex)
            {
                logAction("VolumeProfileRepository Dispose Erreur : " + ex.Message);
            }
        }

        #endregion
    }
}
