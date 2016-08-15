﻿using ARK_Server_Manager.Lib.ViewModel;
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Xml.Serialization;
using TinyCsvParser;
using System.Reflection;
using System.Collections.Generic;
using ARK_Server_Manager.Lib.Model;

namespace ARK_Server_Manager.Lib
{
    public interface ISettingsBag
    {
        object this[string propertyName] { get; set; }
    }

    [XmlRoot("ArkServerProfile")]
    [Serializable]
    public class ServerProfile : DependencyObject
    {
        public enum LevelProgression
        {
            Player,
            Dino
        };

        private const char CSV_DELIMITER = ';';

        [XmlIgnore]
        private string _lastSaveLocation = String.Empty;
        private static NLog.Logger _logger = NLog.LogManager.GetCurrentClassLogger();

        private ServerProfile()
        {
            ServerPassword = SecurityUtils.GeneratePassword(16);
            AdminPassword = SecurityUtils.GeneratePassword(16);

            this.DinoSpawnWeightMultipliers = new AggregateIniValueList<DinoSpawn>(nameof(DinoSpawnWeightMultipliers), GameData.GetDinoSpawns);
            this.PreventDinoTameClassNames = new StringIniValueList(nameof(PreventDinoTameClassNames), () => new string[0] );
            this.NPCReplacements = new AggregateIniValueList<NPCReplacement>(nameof(NPCReplacements), GameData.GetNPCReplacements);
            this.TamedDinoClassDamageMultipliers = new AggregateIniValueList<ClassMultiplier>(nameof(TamedDinoClassDamageMultipliers), GameData.GetStandardDinoMultipliers);
            this.TamedDinoClassResistanceMultipliers = new AggregateIniValueList<ClassMultiplier>(nameof(TamedDinoClassResistanceMultipliers), GameData.GetStandardDinoMultipliers);
            this.DinoClassDamageMultipliers = new AggregateIniValueList<ClassMultiplier>(nameof(DinoClassDamageMultipliers), GameData.GetStandardDinoMultipliers);
            this.DinoClassResistanceMultipliers = new AggregateIniValueList<ClassMultiplier>(nameof(DinoClassResistanceMultipliers), GameData.GetStandardDinoMultipliers);
            this.DinoSettings = new DinoSettingsList(this.DinoSpawnWeightMultipliers, this.PreventDinoTameClassNames, this.NPCReplacements, this.TamedDinoClassDamageMultipliers, this.TamedDinoClassResistanceMultipliers, this.DinoClassDamageMultipliers, this.DinoClassResistanceMultipliers);

            this.HarvestResourceItemAmountClassMultipliers = new AggregateIniValueList<ResourceClassMultiplier>(nameof(HarvestResourceItemAmountClassMultipliers), GameData.GetStandardResourceMultipliers);
            this.OverrideNamedEngramEntries = new AggregateIniValueList<EngramEntry>(nameof(OverrideNamedEngramEntries), GameData.GetStandardEngramOverrides);

            this.DinoLevels = new LevelList();
            this.PlayerLevels = new LevelList();
            this.PerLevelStatsMultiplier_Player = new FloatIniValueArray(nameof(PerLevelStatsMultiplier_Player), GameData.GetPerLevelStatsMultipliers_Default);
            this.PerLevelStatsMultiplier_DinoWild = new FloatIniValueArray(nameof(PerLevelStatsMultiplier_DinoWild), GameData.GetPerLevelStatsMultipliers_Default);
            this.PerLevelStatsMultiplier_DinoTamed = new FloatIniValueArray(nameof(PerLevelStatsMultiplier_DinoTamed), GameData.GetPerLevelStatsMultipliers_DinoTamed);
            this.PerLevelStatsMultiplier_DinoTamed_Add = new FloatIniValueArray(nameof(PerLevelStatsMultiplier_DinoTamed_Add), GameData.GetPerLevelStatsMultipliers_DinoTamed_Add);
            this.PerLevelStatsMultiplier_DinoTamed_Affinity = new FloatIniValueArray(nameof(PerLevelStatsMultiplier_DinoTamed_Affinity), GameData.GetPerLevelStatsMultipliers_DinoTamed_Affinity);

            //this.ConfigOverrideItemCraftingCosts = new AggregateIniValueList<Crafting>(nameof(ConfigOverrideItemCraftingCosts), null);
            //this.CustomGameSections = new CustomSectionList();
            this.CustomGameUserSettingsSections = new CustomSectionList();

            GetDefaultDirectories();
        }

        #region Properties
        public static readonly DependencyProperty IsDirtyProperty = DependencyProperty.Register(nameof(IsDirty), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        [XmlIgnore]
        public bool IsDirty
        {
            get { return (bool)GetValue(IsDirtyProperty); }
            set { SetValue(IsDirtyProperty, value); }
        }

        public static readonly DependencyProperty ProfileNameProperty = DependencyProperty.Register(nameof(ProfileName), typeof(string), typeof(ServerProfile), new PropertyMetadata(Config.Default.DefaultServerProfileName));
        public string ProfileName
        {
            get { return (string)GetValue(ProfileNameProperty); }
            set { SetValue(ProfileNameProperty, value); }
        }

        public static readonly DependencyProperty InstallDirectoryProperty = DependencyProperty.Register(nameof(InstallDirectory), typeof(string), typeof(ServerProfile), new PropertyMetadata(String.Empty));
        public string InstallDirectory
        {
            get { return (string)GetValue(InstallDirectoryProperty); }
            set { SetValue(InstallDirectoryProperty, value); }
        }

        public static readonly DependencyProperty LastInstalledVersionProperty = DependencyProperty.Register(nameof(LastInstalledVersion), typeof(string), typeof(ServerProfile), new PropertyMetadata(new Version(0, 0).ToString()));
        public string LastInstalledVersion
        {
            get { return (string)GetValue(LastInstalledVersionProperty); }
            set { SetValue(LastInstalledVersionProperty, value); }
        }

        #region Administration
        public static readonly DependencyProperty ServerNameProperty = DependencyProperty.Register(nameof(ServerName), typeof(string), typeof(ServerProfile), new PropertyMetadata(Config.Default.DefaultServerName));
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.SessionSettings, "SessionName")]
        public string ServerName
        {
            get { return (string)GetValue(ServerNameProperty); }
            set { SetValue(ServerNameProperty, value); }
        }

        public static readonly DependencyProperty ServerPasswordProperty = DependencyProperty.Register(nameof(ServerPassword), typeof(string), typeof(ServerProfile), new PropertyMetadata(String.Empty));
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings, "ServerPassword")]
        public string ServerPassword
        {
            get { return (string)GetValue(ServerPasswordProperty); }
            set { SetValue(ServerPasswordProperty, value); }
        }

        public static readonly DependencyProperty AdminPasswordProperty = DependencyProperty.Register(nameof(AdminPassword), typeof(string), typeof(ServerProfile), new PropertyMetadata(String.Empty));
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings, "ServerAdminPassword")]
        public string AdminPassword
        {
            get { return (string)GetValue(AdminPasswordProperty); }
            set { SetValue(AdminPasswordProperty, value); }
        }

        public static readonly DependencyProperty SpectatorPasswordProperty = DependencyProperty.Register(nameof(SpectatorPassword), typeof(string), typeof(ServerProfile), new PropertyMetadata(String.Empty));
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings)]
        public string SpectatorPassword
        {
            get { return (string)GetValue(SpectatorPasswordProperty); }
            set { SetValue(SpectatorPasswordProperty, value); }
        }

        public static readonly DependencyProperty ServerConnectionPortProperty = DependencyProperty.Register(nameof(ServerConnectionPort), typeof(int), typeof(ServerProfile), new PropertyMetadata(7777));
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.SessionSettings, "Port")]
        public int ServerConnectionPort
        {
            get { return (int)GetValue(ServerConnectionPortProperty); }
            set { SetValue(ServerConnectionPortProperty, value); }
        }

        public static readonly DependencyProperty ServerPortProperty = DependencyProperty.Register(nameof(ServerPort), typeof(int), typeof(ServerProfile), new PropertyMetadata(27015));
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.SessionSettings, "QueryPort")]
        public int ServerPort
        {
            get { return (int)GetValue(ServerPortProperty); }
            set { SetValue(ServerPortProperty, value); }
        }

        public static readonly DependencyProperty ServerIPProperty = DependencyProperty.Register(nameof(ServerIP), typeof(string), typeof(ServerProfile), new PropertyMetadata(String.Empty));
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.SessionSettings, "MultiHome")]
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.MultiHome, "MultiHome", WriteBoolValueIfNonEmpty = true)]
        public string ServerIP
        {
            get { return (string)GetValue(ServerIPProperty); }
            set { SetValue(ServerIPProperty, value); }
        }

        public static readonly DependencyProperty UseRawSocketsProperty = DependencyProperty.Register(nameof(UseRawSockets), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        public bool UseRawSockets
        {
            get { return (bool)GetValue(UseRawSocketsProperty); }
            set { SetValue(UseRawSocketsProperty, value); }
        }

        public static readonly DependencyProperty EnableBanListURLProperty = DependencyProperty.Register(nameof(EnableBanListURL), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        public bool EnableBanListURL
        {
            get { return (bool)GetValue(EnableBanListURLProperty); }
            set { SetValue(EnableBanListURLProperty, value); }
        }

        public static readonly DependencyProperty BanListURLProperty = DependencyProperty.Register(nameof(BanListURL), typeof(string), typeof(ServerProfile), new PropertyMetadata("\"http://playark.com/banlist.txt\""));
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings, ConditionedOn = nameof(EnableBanListURL), QuotedString = true)]
        public string BanListURL
        {
            get { return (string)GetValue(BanListURLProperty); }
            set { SetValue(BanListURLProperty, value); }
        }

        public static readonly DependencyProperty MaxPlayersProperty = DependencyProperty.Register(nameof(MaxPlayers), typeof(int), typeof(ServerProfile), new PropertyMetadata(70));
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.GameSession, "MaxPlayers")]
        public int MaxPlayers
        {
            get { return (int)GetValue(MaxPlayersProperty); }
            set { SetValue(MaxPlayersProperty, value); }
        }

        public static readonly DependencyProperty EnableKickIdlePlayersProperty = DependencyProperty.Register(nameof(EnableKickIdlePlayers), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        public bool EnableKickIdlePlayers
        {
            get { return (bool)GetValue(EnableKickIdlePlayersProperty); }
            set { SetValue(EnableKickIdlePlayersProperty, value); }
        }

        public static readonly DependencyProperty KickIdlePlayersPeriodProperty = DependencyProperty.Register(nameof(KickIdlePlayersPeriod), typeof(int), typeof(ServerProfile), new PropertyMetadata(3600));
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings, ConditionedOn = nameof(EnableKickIdlePlayers))]
        public int KickIdlePlayersPeriod
        {
            get { return (int)GetValue(KickIdlePlayersPeriodProperty); }
            set { SetValue(KickIdlePlayersPeriodProperty, value); }
        }

        public static readonly DependencyProperty RCONEnabledProperty = DependencyProperty.Register(nameof(RCONEnabled), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings)]
        public bool RCONEnabled
        {
            get { return (bool)GetValue(RCONEnabledProperty); }
            set { SetValue(RCONEnabledProperty, value); }
        }

        public static readonly DependencyProperty RCONPortProperty = DependencyProperty.Register(nameof(RCONPort), typeof(int), typeof(ServerProfile), new PropertyMetadata(32330));
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings)]
        public int RCONPort
        {
            get { return (int)GetValue(RCONPortProperty); }
            set { SetValue(RCONPortProperty, value); }
        }

        public static readonly DependencyProperty RCONServerGameLogBufferProperty = DependencyProperty.Register(nameof(RCONServerGameLogBuffer), typeof(int), typeof(ServerProfile), new PropertyMetadata(600));
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings)]
        public int RCONServerGameLogBuffer
        {
            get { return (int)GetValue(RCONServerGameLogBufferProperty); }
            set { SetValue(RCONServerGameLogBufferProperty, value); }
        }

        public static readonly DependencyProperty AdminLoggingProperty = DependencyProperty.Register(nameof(AdminLogging), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings)]
        public bool AdminLogging
        {
            get { return (bool)GetValue(AdminLoggingProperty); }
            set { SetValue(AdminLoggingProperty, value); }
        }

        public static readonly DependencyProperty ServerMapProperty = DependencyProperty.Register(nameof(ServerMap), typeof(string), typeof(ServerProfile), new PropertyMetadata(Config.Default.DefaultServerMap_TheIsland));
        public string ServerMap
        {
            get { return (string)GetValue(ServerMapProperty); }
            set { SetValue(ServerMapProperty, value); }
        }

        public static readonly DependencyProperty TotalConversionModIdProperty = DependencyProperty.Register(nameof(TotalConversionModId), typeof(string), typeof(ServerProfile), new PropertyMetadata(String.Empty));
        public string TotalConversionModId
        {
            get { return (string)GetValue(TotalConversionModIdProperty); }
            set { SetValue(TotalConversionModIdProperty, value); }
        }

        public static readonly DependencyProperty ServerModIdsProperty = DependencyProperty.Register(nameof(ServerModIds), typeof(string), typeof(ServerProfile), new PropertyMetadata(String.Empty));
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings, Key = "ActiveMods")]
        public string ServerModIds
        {
            get { return (string)GetValue(ServerModIdsProperty); }
            set { SetValue(ServerModIdsProperty, value); }
        }

        public static readonly DependencyProperty EnableExtinctionEventProperty = DependencyProperty.Register(nameof(EnableExtinctionEvent), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        public bool EnableExtinctionEvent
        {
            get { return (bool)GetValue(EnableExtinctionEventProperty); }
            set { SetValue(EnableExtinctionEventProperty, value); }
        }

        public static readonly DependencyProperty ExtinctionEventTimeIntervalProperty = DependencyProperty.Register(nameof(ExtinctionEventTimeInterval), typeof(int), typeof(ServerProfile), new PropertyMetadata(2592000));
        [XmlIgnore]
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings, ConditionedOn = nameof(EnableExtinctionEvent))]
        public int ExtinctionEventTimeInterval
        {
            get { return (int)GetValue(ExtinctionEventTimeIntervalProperty); }
            set { SetValue(ExtinctionEventTimeIntervalProperty, value); }
        }

        public static readonly DependencyProperty ExtinctionEventUTCProperty = DependencyProperty.Register(nameof(ExtinctionEventUTC), typeof(int), typeof(ServerProfile), new PropertyMetadata(0));
        [XmlIgnore]
        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode, "NextExtinctionEventUTC", ClearWhenOff = nameof(EnableExtinctionEvent))]
        public int ExtinctionEventUTC
        {
            get { return (int)GetValue(ExtinctionEventUTCProperty); }
            set { SetValue(ExtinctionEventUTCProperty, value); }
        }

        public static readonly DependencyProperty AutoSavePeriodMinutesProperty = DependencyProperty.Register(nameof(AutoSavePeriodMinutes), typeof(float), typeof(ServerProfile), new PropertyMetadata(15.0f));
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings)]
        public float AutoSavePeriodMinutes
        {
            get { return (float)GetValue(AutoSavePeriodMinutesProperty); }
            set { SetValue(AutoSavePeriodMinutesProperty, value); }
        }

        public static readonly DependencyProperty MOTDProperty = DependencyProperty.Register(nameof(MOTD), typeof(string), typeof(ServerProfile), new PropertyMetadata(String.Empty));
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.MessageOfTheDay, "Message", ClearSection = true, Multiline = true)]
        public string MOTD
        {
            get { return (string)GetValue(MOTDProperty); }
            set { SetValue(MOTDProperty, value); }
        }

        public static readonly DependencyProperty MOTDDurationProperty = DependencyProperty.Register(nameof(MOTDDuration), typeof(int), typeof(ServerProfile), new PropertyMetadata(20));
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.MessageOfTheDay, "Duration")]
        public int MOTDDuration
        {
            get { return (int)GetValue(MOTDDurationProperty); }
            set { SetValue(MOTDDurationProperty, value); }
        }

        public static readonly DependencyProperty DisableValveAntiCheatSystemProperty = DependencyProperty.Register(nameof(DisableValveAntiCheatSystem), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        public bool DisableValveAntiCheatSystem
        {
            get { return (bool)GetValue(DisableValveAntiCheatSystemProperty); }
            set { SetValue(DisableValveAntiCheatSystemProperty, value); }
        }

        public static readonly DependencyProperty DisablePlayerMovePhysicsOptimizationProperty = DependencyProperty.Register(nameof(DisablePlayerMovePhysicsOptimization), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        public bool DisablePlayerMovePhysicsOptimization
        {
            get { return (bool)GetValue(DisablePlayerMovePhysicsOptimizationProperty); }
            set { SetValue(DisablePlayerMovePhysicsOptimizationProperty, value); }
        }

        public static readonly DependencyProperty DisableAntiSpeedHackDetectionProperty = DependencyProperty.Register(nameof(DisableAntiSpeedHackDetection), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        public bool DisableAntiSpeedHackDetection
        {
            get { return (bool)GetValue(DisableAntiSpeedHackDetectionProperty); }
            set { SetValue(DisableAntiSpeedHackDetectionProperty, value); }
        }

        public static readonly DependencyProperty SpeedHackBiasProperty = DependencyProperty.Register(nameof(SpeedHackBias), typeof(float), typeof(ServerProfile), new PropertyMetadata(1.0f));
        public float SpeedHackBias
        {
            get { return (float)GetValue(SpeedHackBiasProperty); }
            set { SetValue(SpeedHackBiasProperty, value); }
        }

        public static readonly DependencyProperty UseBattlEyeProperty = DependencyProperty.Register(nameof(UseBattlEye), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        public bool UseBattlEye
        {
            get { return (bool)GetValue(UseBattlEyeProperty); }
            set { SetValue(UseBattlEyeProperty, value); }
        }

        public static readonly DependencyProperty ForceRespawnDinosProperty = DependencyProperty.Register(nameof(ForceRespawnDinos), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        public bool ForceRespawnDinos
        {
            get { return (bool)GetValue(ForceRespawnDinosProperty); }
            set { SetValue(ForceRespawnDinosProperty, value); }
        }

        public static readonly DependencyProperty EnableServerAdminLogsProperty = DependencyProperty.Register(nameof(EnableServerAdminLogs), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        public bool EnableServerAdminLogs
        {
            get { return (bool)GetValue(EnableServerAdminLogsProperty); }
            set { SetValue(EnableServerAdminLogsProperty, value); }
        }

        public static readonly DependencyProperty MaxTribeLogsProperty = DependencyProperty.Register(nameof(MaxTribeLogs), typeof(int), typeof(ServerProfile), new PropertyMetadata(100));
        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode)]
        public int MaxTribeLogs
        {
            get { return (int)GetValue(MaxTribeLogsProperty); }
            set { SetValue(MaxTribeLogsProperty, value); }
        }

        public static readonly DependencyProperty ForceDirectX10Property = DependencyProperty.Register(nameof(ForceDirectX10), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        public bool ForceDirectX10
        {
            get { return (bool)GetValue(ForceDirectX10Property); }
            set { SetValue(ForceDirectX10Property, value); }
        }

        public static readonly DependencyProperty ForceShaderModel4Property = DependencyProperty.Register(nameof(ForceShaderModel4), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        public bool ForceShaderModel4
        {
            get { return (bool)GetValue(ForceShaderModel4Property); }
            set { SetValue(ForceShaderModel4Property, value); }
        }

        public static readonly DependencyProperty ForceLowMemoryProperty = DependencyProperty.Register(nameof(ForceLowMemory), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        public bool ForceLowMemory
        {
            get { return (bool)GetValue(ForceLowMemoryProperty); }
            set { SetValue(ForceLowMemoryProperty, value); }
        }

        public static readonly DependencyProperty ForceNoManSkyProperty = DependencyProperty.Register(nameof(ForceNoManSky), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        public bool ForceNoManSky
        {
            get { return (bool)GetValue(ForceNoManSkyProperty); }
            set { SetValue(ForceNoManSkyProperty, value); }
        }

        public static readonly DependencyProperty UseAllAvailableCoresProperty = DependencyProperty.Register(nameof(UseAllAvailableCores), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        public bool UseAllAvailableCores
        {
            get { return (bool)GetValue(UseAllAvailableCoresProperty); }
            set { SetValue(UseAllAvailableCoresProperty, value); }
        }

        public static readonly DependencyProperty UseCacheProperty = DependencyProperty.Register(nameof(UseCache), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        public bool UseCache
        {
            get { return (bool)GetValue(UseCacheProperty); }
            set { SetValue(UseCacheProperty, value); }
        }

        public static readonly DependencyProperty EnableWebAlarmProperty = DependencyProperty.Register(nameof(EnableWebAlarm), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        public bool EnableWebAlarm
        {
            get { return (bool)GetValue(EnableWebAlarmProperty); }
            set { SetValue(EnableWebAlarmProperty, value); }
        }

        public static readonly DependencyProperty WebAlarmKeyProperty = DependencyProperty.Register(nameof(WebAlarmKey), typeof(string), typeof(ServerProfile), new PropertyMetadata(String.Empty));
        public string WebAlarmKey
        {
            get { return (string)GetValue(WebAlarmKeyProperty); }
            set { SetValue(WebAlarmKeyProperty, value); }
        }

        public static readonly DependencyProperty WebAlarmUrlProperty = DependencyProperty.Register(nameof(WebAlarmUrl), typeof(string), typeof(ServerProfile), new PropertyMetadata(String.Empty));
        public string WebAlarmUrl
        {
            get { return (string)GetValue(WebAlarmUrlProperty); }
            set { SetValue(WebAlarmUrlProperty, value); }
        }

        public static readonly DependencyProperty AutoManagedModsProperty = DependencyProperty.Register(nameof(AutoManagedMods), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        public bool AutoManagedMods
        {
            get { return (bool)GetValue(AutoManagedModsProperty); }
            set { SetValue(AutoManagedModsProperty, value); }
        }

        public static readonly DependencyProperty UseOldSaveFormatProperty = DependencyProperty.Register(nameof(UseOldSaveFormat), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        public bool UseOldSaveFormat
        {
            get { return (bool)GetValue(UseOldSaveFormatProperty); }
            set { SetValue(UseOldSaveFormatProperty, value); }
        }

        public static readonly DependencyProperty UseNoMemoryBiasProperty = DependencyProperty.Register(nameof(UseNoMemoryBias), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        public bool UseNoMemoryBias
        {
            get { return (bool)GetValue(UseNoMemoryBiasProperty); }
            set { SetValue(UseNoMemoryBiasProperty, value); }
        }

        public static readonly DependencyProperty AdditionalArgsProperty = DependencyProperty.Register(nameof(AdditionalArgs), typeof(string), typeof(ServerProfile), new PropertyMetadata(String.Empty));
        public string AdditionalArgs
        {
            get { return (string)GetValue(AdditionalArgsProperty); }
            set { SetValue(AdditionalArgsProperty, value); }
        }
        #endregion

        #region Automatic Management
        public static readonly DependencyProperty EnableAutoStartProperty = DependencyProperty.Register(nameof(EnableAutoStart), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        public bool EnableAutoStart
        {
            get { return (bool)GetValue(EnableAutoStartProperty); }
            set { SetValue(EnableAutoStartProperty, value); }
        }

        public static readonly DependencyProperty EnableAutoUpdateProperty = DependencyProperty.Register(nameof(EnableAutoUpdate), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        public bool EnableAutoUpdate
        {
            get { return (bool)GetValue(EnableAutoUpdateProperty); }
            set { SetValue(EnableAutoUpdateProperty, value); }
        }

        public static readonly DependencyProperty EnableServerRestartProperty = DependencyProperty.Register(nameof(EnableAutoRestart), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        public bool EnableAutoRestart
        {
            get { return (bool)GetValue(EnableServerRestartProperty); }
            set { SetValue(EnableServerRestartProperty, value); }
        }

        public static readonly DependencyProperty AutoRestartTimeProperty = DependencyProperty.Register(nameof(AutoRestartTime), typeof(string), typeof(ServerProfile), new PropertyMetadata("00:00"));
        public string AutoRestartTime
        {
            get { return (string)GetValue(AutoRestartTimeProperty); }
            set { SetValue(AutoRestartTimeProperty, value); }
        }

        public static readonly DependencyProperty AutoRestartIfShutdownProperty = DependencyProperty.Register(nameof(AutoRestartIfShutdown), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        public bool AutoRestartIfShutdown
        {
            get { return (bool)GetValue(AutoRestartIfShutdownProperty); }
            set { SetValue(AutoRestartIfShutdownProperty, value); }
        }
        #endregion

        #region Rules
        public static readonly DependencyProperty EnableHardcoreProperty = DependencyProperty.Register(nameof(EnableHardcore), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings, "ServerHardcore")]
        public bool EnableHardcore
        {
            get { return (bool)GetValue(EnableHardcoreProperty); }
            set { SetValue(EnableHardcoreProperty, value); }
        }

        public static readonly DependencyProperty EnablePVPProperty = DependencyProperty.Register(nameof(EnablePVP), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings, "ServerPVE", InvertBoolean = true)]
        public bool EnablePVP
        {
            get { return (bool)GetValue(EnablePVPProperty); }
            set { SetValue(EnablePVPProperty, value); }
        }

        public static readonly DependencyProperty AllowCaveBuildingPvEProperty = DependencyProperty.Register(nameof(AllowCaveBuildingPvE), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings)]
        public bool AllowCaveBuildingPvE
        {
            get { return (bool)GetValue(AllowCaveBuildingPvEProperty); }
            set { SetValue(AllowCaveBuildingPvEProperty, value); }
        }

        public static readonly DependencyProperty DisableFriendlyFirePvPProperty = DependencyProperty.Register(nameof(DisableFriendlyFirePvP), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode, "bDisableFriendlyFire")]
        public bool DisableFriendlyFirePvP
        {
            get { return (bool)GetValue(DisableFriendlyFirePvPProperty); }
            set { SetValue(DisableFriendlyFirePvPProperty, value); }
        }

        public static readonly DependencyProperty DisableFriendlyFirePvEProperty = DependencyProperty.Register(nameof(DisableFriendlyFirePvE), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode, "bPvEDisableFriendlyFire")]
        public bool DisableFriendlyFirePvE
        {
            get { return (bool)GetValue(DisableFriendlyFirePvEProperty); }
            set { SetValue(DisableFriendlyFirePvEProperty, value); }
        }

        public static readonly DependencyProperty DisableLootCratesProperty = DependencyProperty.Register(nameof(DisableLootCrates), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode, "bDisableLootCrates")]
        public bool DisableLootCrates
        {
            get { return (bool)GetValue(DisableLootCratesProperty); }
            set { SetValue(DisableLootCratesProperty, value); }
        }

        public static readonly DependencyProperty EnableExtraStructurePreventionVolumesProperty = DependencyProperty.Register(nameof(EnableExtraStructurePreventionVolumes), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings)]
        public bool EnableExtraStructurePreventionVolumes
        {
            get { return (bool)GetValue(EnableExtraStructurePreventionVolumesProperty); }
            set { SetValue(EnableExtraStructurePreventionVolumesProperty, value); }
        }

        public static readonly DependencyProperty DifficultyOffsetProperty = DependencyProperty.Register(nameof(DifficultyOffset), typeof(float), typeof(ServerProfile), new PropertyMetadata(1.0f));
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings, "DifficultyOffset")]
        public float DifficultyOffset
        {
            get { return (float)GetValue(DifficultyOffsetProperty); }
            set { SetValue(DifficultyOffsetProperty, value); }
        }

        public static readonly DependencyProperty MaxNumberOfPlayersInTribeProperty = DependencyProperty.Register(nameof(MaxNumberOfPlayersInTribe), typeof(int), typeof(ServerProfile), new PropertyMetadata(0));
        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode)]
        public int MaxNumberOfPlayersInTribe
        {
            get { return (int)GetValue(MaxNumberOfPlayersInTribeProperty); }
            set { SetValue(MaxNumberOfPlayersInTribeProperty, value); }
        }

        public static readonly DependencyProperty EnableTributeDownloadsProperty = DependencyProperty.Register(nameof(EnableTributeDownloads), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings, "NoTributeDownloads", InvertBoolean = true)]
        public bool EnableTributeDownloads
        {
            get { return (bool)GetValue(EnableTributeDownloadsProperty); }
            set { SetValue(EnableTributeDownloadsProperty, value); }
        }

        public static readonly DependencyProperty PreventDownloadSurvivorsProperty = DependencyProperty.Register(nameof(PreventDownloadSurvivors), typeof(bool), typeof(ServerProfile), new PropertyMetadata(true));
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings)]
        public bool PreventDownloadSurvivors
        {
            get { return (bool)GetValue(PreventDownloadSurvivorsProperty); }
            set { SetValue(PreventDownloadSurvivorsProperty, value); }
        }

        public static readonly DependencyProperty PreventDownloadItemsProperty = DependencyProperty.Register(nameof(PreventDownloadItems), typeof(bool), typeof(ServerProfile), new PropertyMetadata(true));
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings)]
        public bool PreventDownloadItems
        {
            get { return (bool)GetValue(PreventDownloadItemsProperty); }
            set { SetValue(PreventDownloadItemsProperty, value); }
        }

        public static readonly DependencyProperty PreventDownloadDinosProperty = DependencyProperty.Register(nameof(PreventDownloadDinos), typeof(bool), typeof(ServerProfile), new PropertyMetadata(true));
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings)]
        public bool PreventDownloadDinos
        {
            get { return (bool)GetValue(PreventDownloadDinosProperty); }
            set { SetValue(PreventDownloadDinosProperty, value); }
        }

        public static readonly DependencyProperty IncreasePvPRespawnIntervalProperty = DependencyProperty.Register(nameof(IncreasePvPRespawnInterval), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode, Key = "bIncreasePvPRespawnInterval")]
        public bool IncreasePvPRespawnInterval
        {
            get { return (bool)GetValue(IncreasePvPRespawnIntervalProperty); }
            set { SetValue(IncreasePvPRespawnIntervalProperty, value); }
        }

        public static readonly DependencyProperty IncreasePvPRespawnIntervalCheckPeriodProperty = DependencyProperty.Register(nameof(IncreasePvPRespawnIntervalCheckPeriod), typeof(int), typeof(ServerProfile), new PropertyMetadata(300));
        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode, ConditionedOn = nameof(IncreasePvPRespawnInterval))]
        public int IncreasePvPRespawnIntervalCheckPeriod
        {
            get { return (int)GetValue(IncreasePvPRespawnIntervalCheckPeriodProperty); }
            set { SetValue(IncreasePvPRespawnIntervalCheckPeriodProperty, value); }
        }

        public static readonly DependencyProperty IncreasePvPRespawnIntervalMultiplierProperty = DependencyProperty.Register(nameof(IncreasePvPRespawnIntervalMultiplier), typeof(float), typeof(ServerProfile), new PropertyMetadata(1.0f));
        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode, ConditionedOn = nameof(IncreasePvPRespawnInterval))]
        public float IncreasePvPRespawnIntervalMultiplier
        {
            get { return (float)GetValue(IncreasePvPRespawnIntervalMultiplierProperty); }
            set { SetValue(IncreasePvPRespawnIntervalMultiplierProperty, value); }
        }

        public static readonly DependencyProperty IncreasePvPRespawnIntervalBaseAmountProperty = DependencyProperty.Register(nameof(IncreasePvPRespawnIntervalBaseAmount), typeof(int), typeof(ServerProfile), new PropertyMetadata(60));
        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode, ConditionedOn = nameof(IncreasePvPRespawnInterval))]
        public int IncreasePvPRespawnIntervalBaseAmount
        {
            get { return (int)GetValue(IncreasePvPRespawnIntervalBaseAmountProperty); }
            set { SetValue(IncreasePvPRespawnIntervalBaseAmountProperty, value); }
        }

        public static readonly DependencyProperty PreventOfflinePvPProperty = DependencyProperty.Register(nameof(PreventOfflinePvP), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings)]
        public bool PreventOfflinePvP
        {
            get { return (bool)GetValue(PreventOfflinePvPProperty); }
            set { SetValue(PreventOfflinePvPProperty, value); }
        }

        public static readonly DependencyProperty PreventOfflinePvPIntervalProperty = DependencyProperty.Register(nameof(PreventOfflinePvPInterval), typeof(int), typeof(ServerProfile), new PropertyMetadata(900));
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings, ConditionedOn = nameof(PreventOfflinePvP))]
        public int PreventOfflinePvPInterval
        {
            get { return (int)GetValue(PreventOfflinePvPIntervalProperty); }
            set { SetValue(PreventOfflinePvPIntervalProperty, value); }
        }

        public static readonly DependencyProperty AutoPvETimerProperty = DependencyProperty.Register(nameof(AutoPvETimer), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode, Key = "bAutoPvETimer")]
        public bool AutoPvETimer
        {
            get { return (bool)GetValue(AutoPvETimerProperty); }
            set { SetValue(AutoPvETimerProperty, value); }
        }

        public static readonly DependencyProperty AutoPvEUseSystemTimeProperty = DependencyProperty.Register(nameof(AutoPvEUseSystemTime), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode, Key = "bAutoPvEUseSystemTime", ConditionedOn = nameof(AutoPvETimer))]
        public bool AutoPvEUseSystemTime
        {
            get { return (bool)GetValue(AutoPvEUseSystemTimeProperty); }
            set { SetValue(AutoPvEUseSystemTimeProperty, value); }
        }

        public static readonly DependencyProperty AutoPvEStartTimeSecondsProperty = DependencyProperty.Register(nameof(AutoPvEStartTimeSeconds), typeof(int), typeof(ServerProfile), new PropertyMetadata(0));
        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode, ConditionedOn = nameof(AutoPvETimer))]
        public int AutoPvEStartTimeSeconds
        {
            get { return (int)GetValue(AutoPvEStartTimeSecondsProperty); }
            set { SetValue(AutoPvEStartTimeSecondsProperty, value); }
        }

        public static readonly DependencyProperty AutoPvEStopTimeSecondsProperty = DependencyProperty.Register(nameof(AutoPvEStopTimeSeconds), typeof(int), typeof(ServerProfile), new PropertyMetadata(0));
        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode, ConditionedOn = nameof(AutoPvETimer))]
        public int AutoPvEStopTimeSeconds
        {
            get { return (int)GetValue(AutoPvEStopTimeSecondsProperty); }
            set { SetValue(AutoPvEStopTimeSecondsProperty, value); }
        }

        public static readonly DependencyProperty AllowTribeWarPvEProperty = DependencyProperty.Register(nameof(AllowTribeWarPvE), typeof(bool), typeof(ServerProfile), new PropertyMetadata(true));
        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode, "bPvEAllowTribeWar")]
        public bool AllowTribeWarPvE
        {
            get { return (bool)GetValue(AllowTribeWarPvEProperty); }
            set { SetValue(AllowTribeWarPvEProperty, value); }
        }

        public static readonly DependencyProperty AllowTribeWarCancelPvEProperty = DependencyProperty.Register(nameof(AllowTribeWarCancelPvE), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode, "bPvEAllowTribeWarCancel")]
        public bool AllowTribeWarCancelPvE
        {
            get { return (bool)GetValue(AllowTribeWarCancelPvEProperty); }
            set { SetValue(AllowTribeWarCancelPvEProperty, value); }
        }

        public static readonly DependencyProperty AllowTribeAlliancesProperty = DependencyProperty.Register(nameof(AllowTribeAlliances), typeof(bool), typeof(ServerProfile), new PropertyMetadata(true));
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings, "PreventTribeAlliances", InvertBoolean = true)]
        public bool AllowTribeAlliances
        {
            get { return (bool)GetValue(AllowTribeAlliancesProperty); }
            set { SetValue(AllowTribeAlliancesProperty, value); }
        }

        public static readonly DependencyProperty AllowCustomRecipesProperty = DependencyProperty.Register(nameof(AllowCustomRecipes), typeof(bool), typeof(ServerProfile), new PropertyMetadata(true));
        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode, "bAllowCustomRecipes")]
        public bool AllowCustomRecipes
        {
            get { return (bool)GetValue(AllowCustomRecipesProperty); }
            set { SetValue(AllowCustomRecipesProperty, value); }
        }

        public static readonly DependencyProperty CustomRecipeEffectivenessMultiplierProperty = DependencyProperty.Register(nameof(CustomRecipeEffectivenessMultiplier), typeof(float), typeof(ServerProfile), new PropertyMetadata(1.0f));
        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode)]
        public float CustomRecipeEffectivenessMultiplier
        {
            get { return (float)GetValue(CustomRecipeEffectivenessMultiplierProperty); }
            set { SetValue(CustomRecipeEffectivenessMultiplierProperty, value); }
        }

        public static readonly DependencyProperty CustomRecipeSkillMultiplierProperty = DependencyProperty.Register(nameof(CustomRecipeSkillMultiplier), typeof(float), typeof(ServerProfile), new PropertyMetadata(1.0f));
        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode)]
        public float CustomRecipeSkillMultiplier
        {
            get { return (float)GetValue(CustomRecipeSkillMultiplierProperty); }
            set { SetValue(CustomRecipeSkillMultiplierProperty, value); }
        }

        public static readonly DependencyProperty EnableDiseasesProperty = DependencyProperty.Register(nameof(EnableDiseases), typeof(bool), typeof(ServerProfile), new PropertyMetadata(true));
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings, "PreventDiseases", InvertBoolean = true)]
        public bool EnableDiseases
        {
            get { return (bool)GetValue(EnableDiseasesProperty); }
            set { SetValue(EnableDiseasesProperty, value); }
        }

        public static readonly DependencyProperty NonPermanentDiseasesProperty = DependencyProperty.Register(nameof(NonPermanentDiseases), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings, ConditionedOn = nameof(EnableDiseases))]
        public bool NonPermanentDiseases
        {
            get { return (bool)GetValue(NonPermanentDiseasesProperty); }
            set { SetValue(NonPermanentDiseasesProperty, value); }
        }

        public static readonly DependencyProperty CraftXPMultiplierProperty = DependencyProperty.Register(nameof(CraftXPMultiplier), typeof(float), typeof(ServerProfile), new PropertyMetadata(1.0f));
        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode)]
        public float CraftXPMultiplier
        {
            get { return (float)GetValue(CraftXPMultiplierProperty); }
            set { SetValue(CraftXPMultiplierProperty, value); }
        }

        public static readonly DependencyProperty GenericXPMultiplierProperty = DependencyProperty.Register(nameof(GenericXPMultiplier), typeof(float), typeof(ServerProfile), new PropertyMetadata(1.0f));
        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode)]
        public float GenericXPMultiplier
        {
            get { return (float)GetValue(GenericXPMultiplierProperty); }
            set { SetValue(GenericXPMultiplierProperty, value); }
        }

        public static readonly DependencyProperty HarvestXPMultiplierProperty = DependencyProperty.Register(nameof(HarvestXPMultiplier), typeof(float), typeof(ServerProfile), new PropertyMetadata(1.0f));
        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode)]
        public float HarvestXPMultiplier
        {
            get { return (float)GetValue(HarvestXPMultiplierProperty); }
            set { SetValue(HarvestXPMultiplierProperty, value); }
        }

        public static readonly DependencyProperty KillXPMultiplierProperty = DependencyProperty.Register(nameof(KillXPMultiplier), typeof(float), typeof(ServerProfile), new PropertyMetadata(1.0f));
        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode)]
        public float KillXPMultiplier
        {
            get { return (float)GetValue(KillXPMultiplierProperty); }
            set { SetValue(KillXPMultiplierProperty, value); }
        }

        public static readonly DependencyProperty SpecialXPMultiplierProperty = DependencyProperty.Register(nameof(SpecialXPMultiplier), typeof(float), typeof(ServerProfile), new PropertyMetadata(1.0f));
        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode)]
        public float SpecialXPMultiplier
        {
            get { return (float)GetValue(SpecialXPMultiplierProperty); }
            set { SetValue(SpecialXPMultiplierProperty, value); }
        }
        #endregion

        #region Chat and Notifications
        public static readonly DependencyProperty EnableGlobalVoiceChatProperty = DependencyProperty.Register(nameof(EnableGlobalVoiceChat), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings, "globalVoiceChat")]
        public bool EnableGlobalVoiceChat
        {
            get { return (bool)GetValue(EnableGlobalVoiceChatProperty); }
            set { SetValue(EnableGlobalVoiceChatProperty, value); }
        }

        public static readonly DependencyProperty EnableProximityChatProperty = DependencyProperty.Register(nameof(EnableProximityChat), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings, "proximityChat")]
        public bool EnableProximityChat
        {
            get { return (bool)GetValue(EnableProximityChatProperty); }
            set { SetValue(EnableProximityChatProperty, value); }
        }

        public static readonly DependencyProperty EnablePlayerLeaveNotificationsProperty = DependencyProperty.Register(nameof(EnablePlayerLeaveNotifications), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings, "alwaysNotifyPlayerLeft")]
        public bool EnablePlayerLeaveNotifications
        {
            get { return (bool)GetValue(EnablePlayerLeaveNotificationsProperty); }
            set { SetValue(EnablePlayerLeaveNotificationsProperty, value); }
        }

        public static readonly DependencyProperty EnablePlayerJoinedNotificationsProperty = DependencyProperty.Register(nameof(EnablePlayerJoinedNotifications), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings, "alwaysNotifyPlayerJoined")]
        public bool EnablePlayerJoinedNotifications
        {
            get { return (bool)GetValue(EnablePlayerJoinedNotificationsProperty); }
            set { SetValue(EnablePlayerJoinedNotificationsProperty, value); }
        }
        #endregion

        #region HUD and Visuals
        public static readonly DependencyProperty AllowCrosshairProperty = DependencyProperty.Register(nameof(AllowCrosshair), typeof(bool), typeof(ServerProfile), new PropertyMetadata(true));
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings, "ServerCrosshair")]
        public bool AllowCrosshair
        {
            get { return (bool)GetValue(AllowCrosshairProperty); }
            set { SetValue(AllowCrosshairProperty, value); }
        }

        public static readonly DependencyProperty AllowHUDProperty = DependencyProperty.Register(nameof(AllowHUD), typeof(bool), typeof(ServerProfile), new PropertyMetadata(true));
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings, "ServerForceNoHud", InvertBoolean = true)]
        public bool AllowHUD
        {
            get { return (bool)GetValue(AllowHUDProperty); }
            set { SetValue(AllowHUDProperty, value); }
        }

        public static readonly DependencyProperty AllowThirdPersonViewProperty = DependencyProperty.Register(nameof(AllowThirdPersonView), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings, "AllowThirdPersonPlayer")]
        public bool AllowThirdPersonView
        {
            get { return (bool)GetValue(AllowThirdPersonViewProperty); }
            set { SetValue(AllowThirdPersonViewProperty, value); }
        }

        public static readonly DependencyProperty AllowMapPlayerLocationProperty = DependencyProperty.Register(nameof(AllowMapPlayerLocation), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings, "ShowMapPlayerLocation")]
        public bool AllowMapPlayerLocation
        {
            get { return (bool)GetValue(AllowMapPlayerLocationProperty); }
            set { SetValue(AllowMapPlayerLocationProperty, value); }
        }

        public static readonly DependencyProperty AllowPVPGammaProperty = DependencyProperty.Register(nameof(AllowPVPGamma), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings, "EnablePVPGamma")]
        public bool AllowPVPGamma
        {
            get { return (bool)GetValue(AllowPVPGammaProperty); }
            set { SetValue(AllowPVPGammaProperty, value); }
        }

        public static readonly DependencyProperty AllowPvEGammaProperty = DependencyProperty.Register(nameof(AllowPvEGamma), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings, "DisablePvEGamma", InvertBoolean = true)]
        public bool AllowPvEGamma
        {
            get { return (bool)GetValue(AllowPvEGammaProperty); }
            set { SetValue(AllowPvEGammaProperty, value); }
        }

        public static readonly DependencyProperty ShowFloatingDamageTextProperty = DependencyProperty.Register(nameof(ShowFloatingDamageText), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings)]
        public bool ShowFloatingDamageText
        {
            get { return (bool)GetValue(ShowFloatingDamageTextProperty); }
            set { SetValue(ShowFloatingDamageTextProperty, value); }
        }

        public static readonly DependencyProperty AllowHitMarkersProperty = DependencyProperty.Register(nameof(AllowHitMarkers), typeof(bool), typeof(ServerProfile), new PropertyMetadata(true));
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings)]
        public bool AllowHitMarkers
        {
            get { return (bool)GetValue(AllowHitMarkersProperty); }
            set { SetValue(AllowHitMarkersProperty, value); }
        }
        #endregion

        #region Player Settings
        public static readonly DependencyProperty EnableFlyerCarryProperty = DependencyProperty.Register(nameof(EnableFlyerCarry), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings, "AllowFlyerCarryPVE")]
        public bool EnableFlyerCarry
        {
            get { return (bool)GetValue(EnableFlyerCarryProperty); }
            set { SetValue(EnableFlyerCarryProperty, value); }
        }

        public static readonly DependencyProperty XPMultiplierProperty = DependencyProperty.Register(nameof(XPMultiplier), typeof(float), typeof(ServerProfile), new PropertyMetadata(1.0f));
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings)]
        public float XPMultiplier
        {
            get { return (float)GetValue(XPMultiplierProperty); }
            set { SetValue(XPMultiplierProperty, value); }
        }

        public static readonly DependencyProperty OverrideMaxExperiencePointsPlayerProperty = DependencyProperty.Register(nameof(OverrideMaxExperiencePointsPlayer), typeof(int), typeof(ServerProfile), new PropertyMetadata(GameData.DEFAULT_MAX_EXPERIENCE_POINTS_PLAYER));
        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode)]
        public int OverrideMaxExperiencePointsPlayer
        {
            get { return (int)GetValue(OverrideMaxExperiencePointsPlayerProperty); }
            set { SetValue(OverrideMaxExperiencePointsPlayerProperty, value); }
        }

        public static readonly DependencyProperty PlayerDamageMultiplierProperty = DependencyProperty.Register(nameof(PlayerDamageMultiplier), typeof(float), typeof(ServerProfile), new PropertyMetadata(1.0f));
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings)]
        public float PlayerDamageMultiplier
        {
            get { return (float)GetValue(PlayerDamageMultiplierProperty); }
            set { SetValue(PlayerDamageMultiplierProperty, value); }
        }

        public static readonly DependencyProperty PlayerResistanceMultiplierProperty = DependencyProperty.Register(nameof(PlayerResistanceMultiplier), typeof(float), typeof(ServerProfile), new PropertyMetadata(1.0f));
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings)]
        public float PlayerResistanceMultiplier
        {
            get { return (float)GetValue(PlayerResistanceMultiplierProperty); }
            set { SetValue(PlayerResistanceMultiplierProperty, value); }
        }

        public static readonly DependencyProperty PlayerCharacterWaterDrainMultiplierProperty = DependencyProperty.Register(nameof(PlayerCharacterWaterDrainMultiplier), typeof(float), typeof(ServerProfile), new PropertyMetadata(1.0f));
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings)]
        public float PlayerCharacterWaterDrainMultiplier
        {
            get { return (float)GetValue(PlayerCharacterWaterDrainMultiplierProperty); }
            set { SetValue(PlayerCharacterWaterDrainMultiplierProperty, value); }
        }

        public static readonly DependencyProperty PlayerCharacterFoodDrainMultiplierProperty = DependencyProperty.Register(nameof(PlayerCharacterFoodDrainMultiplier), typeof(float), typeof(ServerProfile), new PropertyMetadata(1.0f));
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings)]
        public float PlayerCharacterFoodDrainMultiplier
        {
            get { return (float)GetValue(PlayerCharacterFoodDrainMultiplierProperty); }
            set { SetValue(PlayerCharacterFoodDrainMultiplierProperty, value); }
        }

        public static readonly DependencyProperty PlayerCharacterStaminaDrainMultiplierProperty = DependencyProperty.Register(nameof(PlayerCharacterStaminaDrainMultiplier), typeof(float), typeof(ServerProfile), new PropertyMetadata(1.0f));
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings)]
        public float PlayerCharacterStaminaDrainMultiplier
        {
            get { return (float)GetValue(PlayerCharacterStaminaDrainMultiplierProperty); }
            set { SetValue(PlayerCharacterStaminaDrainMultiplierProperty, value); }
        }

        public static readonly DependencyProperty PlayerCharacterHealthRecoveryMultiplierProperty = DependencyProperty.Register(nameof(PlayerCharacterHealthRecoveryMultiplier), typeof(float), typeof(ServerProfile), new PropertyMetadata(1.0f));
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings)]
        public float PlayerCharacterHealthRecoveryMultiplier
        {
            get { return (float)GetValue(PlayerCharacterHealthRecoveryMultiplierProperty); }
            set { SetValue(PlayerCharacterHealthRecoveryMultiplierProperty, value); }
        }

        public static readonly DependencyProperty HarvestingDamageMultiplierPlayerProperty = DependencyProperty.Register(nameof(PlayerHarvestingDamageMultiplier), typeof(float), typeof(ServerProfile), new PropertyMetadata(1.0f));
        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode)]
        public float PlayerHarvestingDamageMultiplier
        {
            get { return (float)GetValue(HarvestingDamageMultiplierPlayerProperty); }
            set { SetValue(HarvestingDamageMultiplierPlayerProperty, value); }
        }

        public static readonly DependencyProperty PerLevelStatsMultiplier_PlayerProperty = DependencyProperty.Register(nameof(PerLevelStatsMultiplier_Player), typeof(FloatIniValueArray), typeof(ServerProfile), new PropertyMetadata(null));
        [XmlIgnore]
        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode)]
        public FloatIniValueArray PerLevelStatsMultiplier_Player
        {
            get { return (FloatIniValueArray)GetValue(PerLevelStatsMultiplier_PlayerProperty); }
            set { SetValue(PerLevelStatsMultiplier_PlayerProperty, value); }
        }
        #endregion

        #region Dino Settings
        public static readonly DependencyProperty OverrideMaxExperiencePointsDinoProperty = DependencyProperty.Register(nameof(OverrideMaxExperiencePointsDino), typeof(int), typeof(ServerProfile), new PropertyMetadata(GameData.DEFAULT_MAX_EXPERIENCE_POINTS_DINO));
        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode)]
        public int OverrideMaxExperiencePointsDino
        {
            get { return (int)GetValue(OverrideMaxExperiencePointsDinoProperty); }
            set { SetValue(OverrideMaxExperiencePointsDinoProperty, value); }
        }

        public static readonly DependencyProperty DinoDamageMultiplierProperty = DependencyProperty.Register(nameof(DinoDamageMultiplier), typeof(float), typeof(ServerProfile), new PropertyMetadata(1.0f));
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings)]
        public float DinoDamageMultiplier
        {
            get { return (float)GetValue(DinoDamageMultiplierProperty); }
            set { SetValue(DinoDamageMultiplierProperty, value); }
        }

        public static readonly DependencyProperty TamedDinoDamageMultiplierProperty = DependencyProperty.Register(nameof(TamedDinoDamageMultiplier), typeof(float), typeof(ServerProfile), new PropertyMetadata(1.0f));
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings)]
        public float TamedDinoDamageMultiplier
        {
            get { return (float)GetValue(TamedDinoDamageMultiplierProperty); }
            set { SetValue(TamedDinoDamageMultiplierProperty, value); }
        }

        public static readonly DependencyProperty DinoResistanceMultiplierProperty = DependencyProperty.Register(nameof(DinoResistanceMultiplier), typeof(float), typeof(ServerProfile), new PropertyMetadata(1.0f));
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings)]
        public float DinoResistanceMultiplier
        {
            get { return (float)GetValue(DinoResistanceMultiplierProperty); }
            set { SetValue(DinoResistanceMultiplierProperty, value); }
        }

        public static readonly DependencyProperty TamedDinoResistanceMultiplierProperty = DependencyProperty.Register(nameof(TamedDinoResistanceMultiplier), typeof(float), typeof(ServerProfile), new PropertyMetadata(1.0f));
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings)]
        public float TamedDinoResistanceMultiplier
        {
            get { return (float)GetValue(TamedDinoResistanceMultiplierProperty); }
            set { SetValue(TamedDinoResistanceMultiplierProperty, value); }
        }

        public static readonly DependencyProperty MaxTamedDinosProperty = DependencyProperty.Register(nameof(MaxTamedDinos), typeof(int), typeof(ServerProfile), new PropertyMetadata(4000));
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings)]
        public int MaxTamedDinos
        {
            get { return (int)GetValue(MaxTamedDinosProperty); }
            set { SetValue(MaxTamedDinosProperty, value); }
        }

        public static readonly DependencyProperty DinoCharacterFoodDrainMultiplierProperty = DependencyProperty.Register(nameof(DinoCharacterFoodDrainMultiplier), typeof(float), typeof(ServerProfile), new PropertyMetadata(1.0f));
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings)]
        public float DinoCharacterFoodDrainMultiplier
        {
            get { return (float)GetValue(DinoCharacterFoodDrainMultiplierProperty); }
            set { SetValue(DinoCharacterFoodDrainMultiplierProperty, value); }
        }

        public static readonly DependencyProperty DinoCharacterStaminaDrainMultiplierProperty = DependencyProperty.Register(nameof(DinoCharacterStaminaDrainMultiplier), typeof(float), typeof(ServerProfile), new PropertyMetadata(1.0f));
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings)]
        public float DinoCharacterStaminaDrainMultiplier
        {
            get { return (float)GetValue(DinoCharacterStaminaDrainMultiplierProperty); }
            set { SetValue(DinoCharacterStaminaDrainMultiplierProperty, value); }
        }

        public static readonly DependencyProperty DinoCharacterHealthRecoveryMultiplierProperty = DependencyProperty.Register(nameof(DinoCharacterHealthRecoveryMultiplier), typeof(float), typeof(ServerProfile), new PropertyMetadata(1.0f));
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings)]
        public float DinoCharacterHealthRecoveryMultiplier
        {
            get { return (float)GetValue(DinoCharacterHealthRecoveryMultiplierProperty); }
            set { SetValue(DinoCharacterHealthRecoveryMultiplierProperty, value); }
        }

        public static readonly DependencyProperty DinoCountMultiplierProperty = DependencyProperty.Register(nameof(DinoCountMultiplier), typeof(float), typeof(ServerProfile), new PropertyMetadata(1.0f));
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings)]
        public float DinoCountMultiplier
        {
            get { return (float)GetValue(DinoCountMultiplierProperty); }
            set { SetValue(DinoCountMultiplierProperty, value); }
        }

        public static readonly DependencyProperty HarvestingDamageMultiplierDinoProperty = DependencyProperty.Register(nameof(DinoHarvestingDamageMultiplier), typeof(float), typeof(ServerProfile), new PropertyMetadata(3.0f));
        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode)]
        public float DinoHarvestingDamageMultiplier
        {
            get { return (float)GetValue(HarvestingDamageMultiplierDinoProperty); }
            set { SetValue(HarvestingDamageMultiplierDinoProperty, value); }
        }

        public static readonly DependencyProperty TurretDamageMultiplierDinoProperty = DependencyProperty.Register(nameof(DinoTurretDamageMultiplier), typeof(float), typeof(ServerProfile), new PropertyMetadata(1.0f));
        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode)]
        public float DinoTurretDamageMultiplier
        {
            get { return (float)GetValue(TurretDamageMultiplierDinoProperty); }
            set { SetValue(TurretDamageMultiplierDinoProperty, value); }
        }

        public static readonly DependencyProperty AllowRaidDinoFeedingProperty = DependencyProperty.Register(nameof(AllowRaidDinoFeeding), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings)]
        public bool AllowRaidDinoFeeding
        {
            get { return (bool)GetValue(AllowRaidDinoFeedingProperty); }
            set { SetValue(AllowRaidDinoFeedingProperty, value); }
        }

        public static readonly DependencyProperty RaidDinoCharacterFoodDrainMultiplierProperty = DependencyProperty.Register(nameof(RaidDinoCharacterFoodDrainMultiplier), typeof(float), typeof(ServerProfile), new PropertyMetadata(1.0f));
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings)]
        public float RaidDinoCharacterFoodDrainMultiplier
        {
            get { return (float)GetValue(RaidDinoCharacterFoodDrainMultiplierProperty); }
            set { SetValue(RaidDinoCharacterFoodDrainMultiplierProperty, value); }
        }

        public static readonly DependencyProperty EnableAllowCaveFlyersProperty = DependencyProperty.Register(nameof(EnableAllowCaveFlyers), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        public bool EnableAllowCaveFlyers
        {
            get { return (bool)GetValue(EnableAllowCaveFlyersProperty); }
            set { SetValue(EnableAllowCaveFlyersProperty, value); }
        }

        public static readonly DependencyProperty EnableNoFishLootProperty = DependencyProperty.Register(nameof(EnableNoFishLoot), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        public bool EnableNoFishLoot
        {
            get { return (bool)GetValue(EnableNoFishLootProperty); }
            set { SetValue(EnableNoFishLootProperty, value); }
        }

        public static readonly DependencyProperty DisableDinoDecayPvEProperty = DependencyProperty.Register(nameof(DisableDinoDecayPvE), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings)]
        public bool DisableDinoDecayPvE
        {
            get { return (bool)GetValue(DisableDinoDecayPvEProperty); }
            set { SetValue(DisableDinoDecayPvEProperty, value); }
        }

        public static readonly DependencyProperty PvEDinoDecayPeriodMultiplierProperty = DependencyProperty.Register(nameof(PvEDinoDecayPeriodMultiplier), typeof(float), typeof(ServerProfile), new PropertyMetadata(1.0f));
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings)]
        public float PvEDinoDecayPeriodMultiplier
        {
            get { return (float)GetValue(PvEDinoDecayPeriodMultiplierProperty); }
            set { SetValue(PvEDinoDecayPeriodMultiplierProperty, value); }
        }

        public static readonly DependencyProperty DinoSettingsProperty = DependencyProperty.Register(nameof(DinoSettings), typeof(DinoSettingsList), typeof(ServerProfile), new PropertyMetadata(null));
        [XmlIgnore]
        public DinoSettingsList DinoSettings
        {
            get { return (DinoSettingsList)GetValue(DinoSettingsProperty); }
            set { SetValue(DinoSettingsProperty, value); }
        }

        public static readonly DependencyProperty PerLevelStatsMultiplier_DinoWildProperty = DependencyProperty.Register(nameof(PerLevelStatsMultiplier_DinoWild), typeof(FloatIniValueArray), typeof(ServerProfile), new PropertyMetadata(null));
        [XmlIgnore]
        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode)]
        public FloatIniValueArray PerLevelStatsMultiplier_DinoWild
        {
            get { return (FloatIniValueArray)GetValue(PerLevelStatsMultiplier_DinoWildProperty); }
            set { SetValue(PerLevelStatsMultiplier_DinoWildProperty, value); }
        }

        public static readonly DependencyProperty PerLevelStatsMultiplier_DinoTamedProperty = DependencyProperty.Register(nameof(PerLevelStatsMultiplier_DinoTamed), typeof(FloatIniValueArray), typeof(ServerProfile), new PropertyMetadata(null));
        [XmlIgnore]
        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode)]
        public FloatIniValueArray PerLevelStatsMultiplier_DinoTamed
        {
            get { return (FloatIniValueArray)GetValue(PerLevelStatsMultiplier_DinoTamedProperty); }
            set { SetValue(PerLevelStatsMultiplier_DinoTamedProperty, value); }
        }

        public static readonly DependencyProperty PerLevelStatsMultiplier_DinoTamed_AddProperty = DependencyProperty.Register(nameof(PerLevelStatsMultiplier_DinoTamed_Add), typeof(FloatIniValueArray), typeof(ServerProfile), new PropertyMetadata(null));
        [XmlIgnore]
        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode)]
        public FloatIniValueArray PerLevelStatsMultiplier_DinoTamed_Add
        {
            get { return (FloatIniValueArray)GetValue(PerLevelStatsMultiplier_DinoTamed_AddProperty); }
            set { SetValue(PerLevelStatsMultiplier_DinoTamed_AddProperty, value); }
        }

        public static readonly DependencyProperty PerLevelStatsMultiplier_DinoTamed_AffinityProperty = DependencyProperty.Register(nameof(PerLevelStatsMultiplier_DinoTamed_Affinity), typeof(FloatIniValueArray), typeof(ServerProfile), new PropertyMetadata(null));
        [XmlIgnore]
        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode)]
        public FloatIniValueArray PerLevelStatsMultiplier_DinoTamed_Affinity
        {
            get { return (FloatIniValueArray)GetValue(PerLevelStatsMultiplier_DinoTamed_AffinityProperty); }
            set { SetValue(PerLevelStatsMultiplier_DinoTamed_AffinityProperty, value); }
        }

        public static readonly DependencyProperty MatingIntervalMultiplierProperty = DependencyProperty.Register(nameof(MatingIntervalMultiplier), typeof(float), typeof(ServerProfile), new PropertyMetadata(1.0f));
        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode)]
        public float MatingIntervalMultiplier
        {
            get { return (float)GetValue(MatingIntervalMultiplierProperty); }
            set { SetValue(MatingIntervalMultiplierProperty, value); }
        }

        public static readonly DependencyProperty EggHatchSpeedMultiplierProperty = DependencyProperty.Register(nameof(EggHatchSpeedMultiplier), typeof(float), typeof(ServerProfile), new PropertyMetadata(1.0f));
        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode)]
        public float EggHatchSpeedMultiplier
        {
            get { return (float)GetValue(EggHatchSpeedMultiplierProperty); }
            set { SetValue(EggHatchSpeedMultiplierProperty, value); }
        }

        public static readonly DependencyProperty BabyMatureSpeedMultiplierProperty = DependencyProperty.Register(nameof(BabyMatureSpeedMultiplier), typeof(float), typeof(ServerProfile), new PropertyMetadata(1.0f));
        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode)]
        public float BabyMatureSpeedMultiplier
        {
            get { return (float)GetValue(BabyMatureSpeedMultiplierProperty); }
            set { SetValue(BabyMatureSpeedMultiplierProperty, value); }
        }

        public static readonly DependencyProperty BabyFoodConsumptionSpeedMultiplierProperty = DependencyProperty.Register(nameof(BabyFoodConsumptionSpeedMultiplier), typeof(float), typeof(ServerProfile), new PropertyMetadata(1.0f));
        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode)]
        public float BabyFoodConsumptionSpeedMultiplier
        {
            get { return (float)GetValue(BabyFoodConsumptionSpeedMultiplierProperty); }
            set { SetValue(BabyFoodConsumptionSpeedMultiplierProperty, value); }
        }

        public static readonly DependencyProperty DisableImprintDinoBuffProperty = DependencyProperty.Register(nameof(DisableImprintDinoBuff), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings)]
        public bool DisableImprintDinoBuff
        {
            get { return (bool)GetValue(DisableImprintDinoBuffProperty); }
            set { SetValue(DisableImprintDinoBuffProperty, value); }
        }

        public static readonly DependencyProperty AllowAnyoneBabyImprintCuddleProperty = DependencyProperty.Register(nameof(AllowAnyoneBabyImprintCuddle), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings)]
        public bool AllowAnyoneBabyImprintCuddle
        {
            get { return (bool)GetValue(AllowAnyoneBabyImprintCuddleProperty); }
            set { SetValue(AllowAnyoneBabyImprintCuddleProperty, value); }
        }

        public static readonly DependencyProperty BabyImprintingStatScaleMultiplierProperty = DependencyProperty.Register(nameof(BabyImprintingStatScaleMultiplier), typeof(float), typeof(ServerProfile), new PropertyMetadata(1.0f));
        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode)]
        public float BabyImprintingStatScaleMultiplier
        {
            get { return (float)GetValue(BabyImprintingStatScaleMultiplierProperty); }
            set { SetValue(BabyImprintingStatScaleMultiplierProperty, value); }
        }

        public static readonly DependencyProperty BabyCuddleIntervalMultiplierProperty = DependencyProperty.Register(nameof(BabyCuddleIntervalMultiplier), typeof(float), typeof(ServerProfile), new PropertyMetadata(1.0f));
        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode)]
        public float BabyCuddleIntervalMultiplier
        {
            get { return (float)GetValue(BabyCuddleIntervalMultiplierProperty); }
            set { SetValue(BabyCuddleIntervalMultiplierProperty, value); }
        }

        public static readonly DependencyProperty BabyCuddleGracePeriodMultiplierProperty = DependencyProperty.Register(nameof(BabyCuddleGracePeriodMultiplier), typeof(float), typeof(ServerProfile), new PropertyMetadata(1.0f));
        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode)]
        public float BabyCuddleGracePeriodMultiplier
        {
            get { return (float)GetValue(BabyCuddleGracePeriodMultiplierProperty); }
            set { SetValue(BabyCuddleGracePeriodMultiplierProperty, value); }
        }

        public static readonly DependencyProperty BabyCuddleLoseImprintQualitySpeedMultiplierProperty = DependencyProperty.Register(nameof(BabyCuddleLoseImprintQualitySpeedMultiplier), typeof(float), typeof(ServerProfile), new PropertyMetadata(1.0f));
        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode)]
        public float BabyCuddleLoseImprintQualitySpeedMultiplier
        {
            get { return (float)GetValue(BabyCuddleLoseImprintQualitySpeedMultiplierProperty); }
            set { SetValue(BabyCuddleLoseImprintQualitySpeedMultiplierProperty, value); }
        }



        public static readonly DependencyProperty DinoSpawnsProperty = DependencyProperty.Register(nameof(DinoSpawnWeightMultipliers), typeof(AggregateIniValueList<DinoSpawn>), typeof(ServerProfile), new PropertyMetadata(null));
        [XmlIgnore]
        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode)]
        public AggregateIniValueList<DinoSpawn> DinoSpawnWeightMultipliers
        {
            get { return (AggregateIniValueList<DinoSpawn>)GetValue(DinoSpawnsProperty); }
            set { SetValue(DinoSpawnsProperty, value); }
        }

        public static readonly DependencyProperty TamedDinoClassDamageMultipliersProperty = DependencyProperty.Register(nameof(TamedDinoClassDamageMultipliers), typeof(AggregateIniValueList<ClassMultiplier>), typeof(ServerProfile), new PropertyMetadata(null));
        [XmlIgnore]
        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode)]
        public AggregateIniValueList<ClassMultiplier> TamedDinoClassDamageMultipliers
        {
            get { return (AggregateIniValueList<ClassMultiplier>)GetValue(TamedDinoClassDamageMultipliersProperty); }
            set { SetValue(TamedDinoClassDamageMultipliersProperty, value); }
        }

        public static readonly DependencyProperty TamedDinoClassResistanceMultipliersProperty = DependencyProperty.Register(nameof(TamedDinoClassResistanceMultipliers), typeof(AggregateIniValueList<ClassMultiplier>), typeof(ServerProfile), new PropertyMetadata(null));
        [XmlIgnore]
        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode)]
        public AggregateIniValueList<ClassMultiplier> TamedDinoClassResistanceMultipliers
        {
            get { return (AggregateIniValueList<ClassMultiplier>)GetValue(TamedDinoClassResistanceMultipliersProperty); }
            set { SetValue(TamedDinoClassResistanceMultipliersProperty, value); }
        }

        public static readonly DependencyProperty DinoClassDamageMultipliersProperty = DependencyProperty.Register(nameof(DinoClassDamageMultipliers), typeof(AggregateIniValueList<ClassMultiplier>), typeof(ServerProfile), new PropertyMetadata(null));
        [XmlIgnore]
        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode)]
        public AggregateIniValueList<ClassMultiplier> DinoClassDamageMultipliers
        {
            get { return (AggregateIniValueList<ClassMultiplier>)GetValue(DinoClassDamageMultipliersProperty); }
            set { SetValue(DinoClassDamageMultipliersProperty, value); }
        }

        public static readonly DependencyProperty DinoClassResistanceMultipliersProperty = DependencyProperty.Register(nameof(DinoClassResistanceMultipliers), typeof(AggregateIniValueList<ClassMultiplier>), typeof(ServerProfile), new PropertyMetadata(null));
        [XmlIgnore]
        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode)]
        public AggregateIniValueList<ClassMultiplier> DinoClassResistanceMultipliers
        {
            get { return (AggregateIniValueList<ClassMultiplier>)GetValue(DinoClassResistanceMultipliersProperty); }
            set { SetValue(DinoClassResistanceMultipliersProperty, value); }
        }

        public static readonly DependencyProperty NPCReplacementsProperty = DependencyProperty.Register(nameof(NPCReplacements), typeof(AggregateIniValueList<NPCReplacement>), typeof(ServerProfile), new PropertyMetadata(null));
        [XmlIgnore]
        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode)]
        public AggregateIniValueList<NPCReplacement> NPCReplacements
        {
            get { return (AggregateIniValueList<NPCReplacement>)GetValue(NPCReplacementsProperty); }
            set { SetValue(NPCReplacementsProperty, value); }
        }

        public static readonly DependencyProperty PreventDinoTameClassNamesProperty = DependencyProperty.Register(nameof(PreventDinoTameClassNames), typeof(StringIniValueList), typeof(ServerProfile), new PropertyMetadata(null));
        [XmlIgnore]
        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode)]
        public StringIniValueList PreventDinoTameClassNames
        {
            get { return (StringIniValueList)GetValue(PreventDinoTameClassNamesProperty); }
            set { SetValue(PreventDinoTameClassNamesProperty, value); }
        }
        #endregion

        #region Environment
        public static readonly DependencyProperty TamingSpeedMultiplierProperty = DependencyProperty.Register(nameof(TamingSpeedMultiplier), typeof(float), typeof(ServerProfile), new PropertyMetadata(1.0f));
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings)]
        public float TamingSpeedMultiplier
        {
            get { return (float)GetValue(TamingSpeedMultiplierProperty); }
            set { SetValue(TamingSpeedMultiplierProperty, value); }
        }

        public static readonly DependencyProperty HarvestAmountMultiplierProperty = DependencyProperty.Register(nameof(HarvestAmountMultiplier), typeof(float), typeof(ServerProfile), new PropertyMetadata(1.0f));
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings)]
        public float HarvestAmountMultiplier
        {
            get { return (float)GetValue(HarvestAmountMultiplierProperty); }
            set { SetValue(HarvestAmountMultiplierProperty, value); }
        }

        public static readonly DependencyProperty ResourcesRespawnPeriodMultiplierProperty = DependencyProperty.Register(nameof(ResourcesRespawnPeriodMultiplier), typeof(float), typeof(ServerProfile), new PropertyMetadata(1.0f));
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings)]
        public float ResourcesRespawnPeriodMultiplier
        {
            get { return (float)GetValue(ResourcesRespawnPeriodMultiplierProperty); }
            set { SetValue(ResourcesRespawnPeriodMultiplierProperty, value); }
        }

        public static readonly DependencyProperty ResourceNoReplenishRadiusPlayersProperty = DependencyProperty.Register(nameof(ResourceNoReplenishRadiusPlayers), typeof(float), typeof(ServerProfile), new PropertyMetadata(1.0f));
        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode)]
        public float ResourceNoReplenishRadiusPlayers
        {
            get { return (float)GetValue(ResourceNoReplenishRadiusPlayersProperty); }
            set { SetValue(ResourceNoReplenishRadiusPlayersProperty, value); }
        }

        public static readonly DependencyProperty ResourceNoReplenishRadiusStructuresProperty = DependencyProperty.Register(nameof(ResourceNoReplenishRadiusStructures), typeof(float), typeof(ServerProfile), new PropertyMetadata(1.0f));
        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode)]
        public float ResourceNoReplenishRadiusStructures
        {
            get { return (float)GetValue(ResourceNoReplenishRadiusStructuresProperty); }
            set { SetValue(ResourceNoReplenishRadiusStructuresProperty, value); }
        }

        public static readonly DependencyProperty HarvestHealthMultiplierProperty = DependencyProperty.Register(nameof(HarvestHealthMultiplier), typeof(float), typeof(ServerProfile), new PropertyMetadata(1.0f));
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings)]
        public float HarvestHealthMultiplier
        {
            get { return (float)GetValue(HarvestHealthMultiplierProperty); }
            set { SetValue(HarvestHealthMultiplierProperty, value); }
        }

        public static readonly DependencyProperty ClampResourceHarvestDamageProperty = DependencyProperty.Register(nameof(ClampResourceHarvestDamage), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings)]
        public bool ClampResourceHarvestDamage
        {
            get { return (bool)GetValue(ClampResourceHarvestDamageProperty); }
            set { SetValue(ClampResourceHarvestDamageProperty, value); }
        }

        public static readonly DependencyProperty HarvestResourceItemAmountClassMultipliersProperty = DependencyProperty.Register(nameof(HarvestResourceItemAmountClassMultipliers), typeof(AggregateIniValueList<ResourceClassMultiplier>), typeof(ServerProfile), new PropertyMetadata(null));
        [XmlIgnore]
        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode)]
        public AggregateIniValueList<ResourceClassMultiplier> HarvestResourceItemAmountClassMultipliers
        {
            get { return (AggregateIniValueList<ResourceClassMultiplier>)GetValue(HarvestResourceItemAmountClassMultipliersProperty); }
            set { SetValue(HarvestResourceItemAmountClassMultipliersProperty, value); }
        }

        public static readonly DependencyProperty DayCycleSpeedScaleProperty = DependencyProperty.Register(nameof(DayCycleSpeedScale), typeof(float), typeof(ServerProfile), new PropertyMetadata(1.0f));
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings)]
        public float DayCycleSpeedScale
        {
            get { return (float)GetValue(DayCycleSpeedScaleProperty); }
            set { SetValue(DayCycleSpeedScaleProperty, value); }
        }

        public static readonly DependencyProperty DayTimeSpeedScaleProperty = DependencyProperty.Register(nameof(DayTimeSpeedScale), typeof(float), typeof(ServerProfile), new PropertyMetadata(1.0f));
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings)]
        public float DayTimeSpeedScale
        {
            get { return (float)GetValue(DayTimeSpeedScaleProperty); }
            set { SetValue(DayTimeSpeedScaleProperty, value); }
        }

        public static readonly DependencyProperty NightTimeSpeedScaleProperty = DependencyProperty.Register(nameof(NightTimeSpeedScale), typeof(float), typeof(ServerProfile), new PropertyMetadata(1.0f));
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings)]
        public float NightTimeSpeedScale
        {
            get { return (float)GetValue(NightTimeSpeedScaleProperty); }
            set { SetValue(NightTimeSpeedScaleProperty, value); }
        }

        public static readonly DependencyProperty GlobalSpoilingTimeMultiplierProperty = DependencyProperty.Register(nameof(GlobalSpoilingTimeMultiplier), typeof(float), typeof(ServerProfile), new PropertyMetadata(1.0f));
        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode)]
        public float GlobalSpoilingTimeMultiplier
        {
            get { return (float)GetValue(GlobalSpoilingTimeMultiplierProperty); }
            set { SetValue(GlobalSpoilingTimeMultiplierProperty, value); }
        }

        public static readonly DependencyProperty GlobalCorpseDecompositionTimeMultiplierProperty = DependencyProperty.Register(nameof(GlobalCorpseDecompositionTimeMultiplier), typeof(float), typeof(ServerProfile), new PropertyMetadata(1.0f));
        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode)]
        public float GlobalItemDecompositionTimeMultiplier
        {
            get { return (float)GetValue(GlobalItemDecompositionTimeMultiplierProperty); }
            set { SetValue(GlobalItemDecompositionTimeMultiplierProperty, value); }
        }

        public static readonly DependencyProperty GlobalItemDecompositionTimeMultiplierProperty = DependencyProperty.Register(nameof(GlobalItemDecompositionTimeMultiplier), typeof(float), typeof(ServerProfile), new PropertyMetadata(1.0f));
        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode)]
        public float GlobalCorpseDecompositionTimeMultiplier
        {
            get { return (float)GetValue(GlobalCorpseDecompositionTimeMultiplierProperty); }
            set { SetValue(GlobalCorpseDecompositionTimeMultiplierProperty, value); }
        }

        public static readonly DependencyProperty CropDecaySpeedMultiplierProperty = DependencyProperty.Register(nameof(CropDecaySpeedMultiplier), typeof(float), typeof(ServerProfile), new PropertyMetadata(1.0f));
        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode)]
        public float CropDecaySpeedMultiplier
        {
            get { return (float)GetValue(CropDecaySpeedMultiplierProperty); }
            set { SetValue(CropDecaySpeedMultiplierProperty, value); }
        }

        public static readonly DependencyProperty CropGrowthSpeedMultiplierProperty = DependencyProperty.Register(nameof(CropGrowthSpeedMultiplier), typeof(float), typeof(ServerProfile), new PropertyMetadata(1.0f));
        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode)]
        public float CropGrowthSpeedMultiplier
        {
            get { return (float)GetValue(CropGrowthSpeedMultiplierProperty); }
            set { SetValue(CropGrowthSpeedMultiplierProperty, value); }
        }

        public static readonly DependencyProperty LayEggIntervalMultiplierProperty = DependencyProperty.Register(nameof(LayEggIntervalMultiplier), typeof(float), typeof(ServerProfile), new PropertyMetadata(1.0f));
        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode)]
        public float LayEggIntervalMultiplier
        {
            get { return (float)GetValue(LayEggIntervalMultiplierProperty); }
            set { SetValue(LayEggIntervalMultiplierProperty, value); }
        }

        public static readonly DependencyProperty PoopIntervalMultiplierProperty = DependencyProperty.Register(nameof(PoopIntervalMultiplier), typeof(float), typeof(ServerProfile), new PropertyMetadata(1.0f));
        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode)]
        public float PoopIntervalMultiplier
        {
            get { return (float)GetValue(PoopIntervalMultiplierProperty); }
            set { SetValue(PoopIntervalMultiplierProperty, value); }
        }
        #endregion

        #region Structures
        public static readonly DependencyProperty StructureResistanceMultiplierProperty = DependencyProperty.Register(nameof(StructureResistanceMultiplier), typeof(float), typeof(ServerProfile), new PropertyMetadata(1.0f));
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings)]
        public float StructureResistanceMultiplier
        {
            get { return (float)GetValue(StructureResistanceMultiplierProperty); }
            set { SetValue(StructureResistanceMultiplierProperty, value); }
        }

        public static readonly DependencyProperty StructureDamageMultiplierProperty = DependencyProperty.Register(nameof(StructureDamageMultiplier), typeof(float), typeof(ServerProfile), new PropertyMetadata(1.0f));
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings)]
        public float StructureDamageMultiplier
        {
            get { return (float)GetValue(StructureDamageMultiplierProperty); }
            set { SetValue(StructureDamageMultiplierProperty, value); }
        }

        public static readonly DependencyProperty StructureDamageRepairCooldownProperty = DependencyProperty.Register(nameof(StructureDamageRepairCooldown), typeof(int), typeof(ServerProfile), new PropertyMetadata(180));
        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode)]
        public int StructureDamageRepairCooldown
        {
            get { return (int)GetValue(StructureDamageRepairCooldownProperty); }
            set { SetValue(StructureDamageRepairCooldownProperty, value); }
        }

        public static readonly DependencyProperty PvPStructureDecayProperty = DependencyProperty.Register(nameof(PvPStructureDecay), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings)]
        public bool PvPStructureDecay
        {
            get { return (bool)GetValue(PvPStructureDecayProperty); }
            set { SetValue(PvPStructureDecayProperty, value); }
        }

        public static readonly DependencyProperty PvPZoneStructureDamageMultiplierProperty = DependencyProperty.Register(nameof(PvPZoneStructureDamageMultiplier), typeof(float), typeof(ServerProfile), new PropertyMetadata(6.0f));
        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode)]
        public float PvPZoneStructureDamageMultiplier
        {
            get { return (float)GetValue(PvPZoneStructureDamageMultiplierProperty); }
            set { SetValue(PvPZoneStructureDamageMultiplierProperty, value); }
        }

        public static readonly DependencyProperty MaxStructuresVisibleProperty = DependencyProperty.Register(nameof(MaxStructuresVisible), typeof(float), typeof(ServerProfile), new PropertyMetadata(1300f));
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings, "NewMaxStructuresInRange")]
        public float MaxStructuresVisible
        {
            get { return (float)GetValue(MaxStructuresVisibleProperty); }
            set { SetValue(MaxStructuresVisibleProperty, value); }
        }

        public static readonly DependencyProperty PerPlatformMaxStructuresMultiplierProperty = DependencyProperty.Register(nameof(PerPlatformMaxStructuresMultiplier), typeof(float), typeof(ServerProfile), new PropertyMetadata(1.0f));
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings)]
        public float PerPlatformMaxStructuresMultiplier
        {
            get { return (float)GetValue(PerPlatformMaxStructuresMultiplierProperty); }
            set { SetValue(PerPlatformMaxStructuresMultiplierProperty, value); }
        }

        public static readonly DependencyProperty MaxPlatformSaddleStructureLimitProperty = DependencyProperty.Register(nameof(MaxPlatformSaddleStructureLimit), typeof(int), typeof(ServerProfile), new PropertyMetadata(50));
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings)]
        public int MaxPlatformSaddleStructureLimit
        {
            get { return (int)GetValue(MaxPlatformSaddleStructureLimitProperty); }
            set { SetValue(MaxPlatformSaddleStructureLimitProperty, value); }
        }

        public static readonly DependencyProperty OverrideStructurePlatformPreventionProperty = DependencyProperty.Register(nameof(OverrideStructurePlatformPrevention), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings)]
        public bool OverrideStructurePlatformPrevention
        {
            get { return (bool)GetValue(OverrideStructurePlatformPreventionProperty); }
            set { SetValue(OverrideStructurePlatformPreventionProperty, value); }
        }

        public static readonly DependencyProperty FlyerPlatformAllowUnalignedDinoBasingProperty = DependencyProperty.Register(nameof(FlyerPlatformAllowUnalignedDinoBasing), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode, Key = "bFlyerPlatformAllowUnalignedDinoBasing")]
        public bool FlyerPlatformAllowUnalignedDinoBasing
        {
            get { return (bool)GetValue(FlyerPlatformAllowUnalignedDinoBasingProperty); }
            set { SetValue(FlyerPlatformAllowUnalignedDinoBasingProperty, value); }
        }

        public static readonly DependencyProperty EnableStructureDecayPvEProperty = DependencyProperty.Register(nameof(EnableStructureDecayPvE), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings, "bDisableStructureDecayPVE", InvertBoolean = true)]
        public bool EnableStructureDecayPvE
        {
            get { return (bool)GetValue(EnableStructureDecayPvEProperty); }
            set { SetValue(EnableStructureDecayPvEProperty, value); }
        }

        public static readonly DependencyProperty PvEStructureDecayDestructionPeriodProperty = DependencyProperty.Register(nameof(PvEStructureDecayDestructionPeriod), typeof(float), typeof(ServerProfile), new PropertyMetadata(0f));
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings)]
        public float PvEStructureDecayDestructionPeriod
        {
            get { return (float)GetValue(PvEStructureDecayDestructionPeriodProperty); }
            set { SetValue(PvEStructureDecayDestructionPeriodProperty, value); }
        }

        public static readonly DependencyProperty PvEStructureDecayPeriodMultiplierProperty = DependencyProperty.Register(nameof(PvEStructureDecayPeriodMultiplier), typeof(float), typeof(ServerProfile), new PropertyMetadata(1.0f));
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings)]
        public float PvEStructureDecayPeriodMultiplier
        {
            get { return (float)GetValue(PvEStructureDecayPeriodMultiplierProperty); }
            set { SetValue(PvEStructureDecayPeriodMultiplierProperty, value); }
        }

        public static readonly DependencyProperty AutoDestroyOldStructuresMultiplierProperty = DependencyProperty.Register(nameof(AutoDestroyOldStructuresMultiplier), typeof(float), typeof(ServerProfile), new PropertyMetadata(0.0f));
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings)]
        public float AutoDestroyOldStructuresMultiplier
        {
            get { return (float)GetValue(AutoDestroyOldStructuresMultiplierProperty); }
            set { SetValue(AutoDestroyOldStructuresMultiplierProperty, value); }
        }

        public static readonly DependencyProperty ForceAllStructureLockingProperty = DependencyProperty.Register(nameof(ForceAllStructureLocking), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings)]
        public bool ForceAllStructureLocking
        {
            get { return (bool)GetValue(ForceAllStructureLockingProperty); }
            set { SetValue(ForceAllStructureLockingProperty, value); }
        }

        public static readonly DependencyProperty PassiveDefensesDamageRiderlessDinosProperty = DependencyProperty.Register(nameof(PassiveDefensesDamageRiderlessDinos), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode, "bPassiveDefensesDamageRiderlessDinos")]
        public bool PassiveDefensesDamageRiderlessDinos
        {
            get { return (bool)GetValue(PassiveDefensesDamageRiderlessDinosProperty); }
            set { SetValue(PassiveDefensesDamageRiderlessDinosProperty, value); }
        }

        public static readonly DependencyProperty EnableAutoDestroyStructuresProperty = DependencyProperty.Register(nameof(EnableAutoDestroyStructures), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        public bool EnableAutoDestroyStructures
        {
            get { return (bool)GetValue(EnableAutoDestroyStructuresProperty); }
            set { SetValue(EnableAutoDestroyStructuresProperty, value); }
        }

        public static readonly DependencyProperty OnlyDecayUnsnappedCoreStructuresProperty = DependencyProperty.Register(nameof(OnlyDecayUnsnappedCoreStructures), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings)]
        public bool OnlyDecayUnsnappedCoreStructures
        {
            get { return (bool)GetValue(OnlyDecayUnsnappedCoreStructuresProperty); }
            set { SetValue(OnlyDecayUnsnappedCoreStructuresProperty, value); }
        }

        public static readonly DependencyProperty FastDecayUnsnappedCoreStructuresProperty = DependencyProperty.Register(nameof(FastDecayUnsnappedCoreStructures), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings)]
        public bool FastDecayUnsnappedCoreStructures
        {
            get { return (bool)GetValue(FastDecayUnsnappedCoreStructuresProperty); }
            set { SetValue(FastDecayUnsnappedCoreStructuresProperty, value); }
        }
        #endregion

        #region Engrams
        public static readonly DependencyProperty OverrideNamedEngramEntriesProperty = DependencyProperty.Register(nameof(OverrideNamedEngramEntries), typeof(AggregateIniValueList<EngramEntry>), typeof(ServerProfile), new PropertyMetadata(null));
        [XmlIgnore]
        [IniFileEntry(IniFiles.Game, IniFileSections.GameMode)]
        public AggregateIniValueList<EngramEntry> OverrideNamedEngramEntries
        {
            get { return (AggregateIniValueList<EngramEntry>)GetValue(OverrideNamedEngramEntriesProperty); }
            set { SetValue(OverrideNamedEngramEntriesProperty, value); }
        }
        #endregion

        #region Custom Levels
        public static readonly DependencyProperty EnableLevelProgressionsProperty = DependencyProperty.Register(nameof(EnableLevelProgressions), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        public bool EnableLevelProgressions
        {
            get { return (bool)GetValue(EnableLevelProgressionsProperty); }
            set { SetValue(EnableLevelProgressionsProperty, value); }
        }

        public static readonly DependencyProperty PlayerLevelsProperty = DependencyProperty.Register(nameof(PlayerLevels), typeof(LevelList), typeof(ServerProfile), new PropertyMetadata());
        public LevelList PlayerLevels
        {
            get { return (LevelList)GetValue(PlayerLevelsProperty); }
            set { SetValue(PlayerLevelsProperty, value); }
        }

        public static readonly DependencyProperty DinoLevelsProperty = DependencyProperty.Register(nameof(DinoLevels), typeof(LevelList), typeof(ServerProfile), new PropertyMetadata());
        public LevelList DinoLevels
        {
            get { return (LevelList)GetValue(DinoLevelsProperty); }
            set { SetValue(DinoLevelsProperty, value); }
        }
        #endregion

        #region Custom Settings
        //public static readonly DependencyProperty CustomGameSectionsProperty = DependencyProperty.Register(nameof(CustomGameSections), typeof(CustomSectionList), typeof(ServerProfile), new PropertyMetadata(null));
        //[XmlIgnore]
        //[IniFileEntry(IniFiles.Game, IniFileSections.Custom)]
        //public CustomSectionList CustomGameSections
        //{
        //    get { return (CustomSectionList)GetValue(CustomGameSectionsProperty); }
        //    set { SetValue(CustomGameSectionsProperty, value); }
        //}

        public static readonly DependencyProperty CustomGameUserSettingsSectionsProperty = DependencyProperty.Register(nameof(CustomGameUserSettingsSections), typeof(CustomSectionList), typeof(ServerProfile), new PropertyMetadata(null));
        [XmlIgnore]
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.Custom)]
        public CustomSectionList CustomGameUserSettingsSections
        {
            get { return (CustomSectionList)GetValue(CustomGameUserSettingsSectionsProperty); }
            set { SetValue(CustomGameUserSettingsSectionsProperty, value); }
        }
        #endregion

        #region Survival of the Fittest
        public static readonly DependencyProperty SOTF_EnabledProperty = DependencyProperty.Register(nameof(SOTF_Enabled), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        public bool SOTF_Enabled
        {
            get { return (bool)GetValue(SOTF_EnabledProperty); }
            set { SetValue(SOTF_EnabledProperty, value); }
        }

        public static readonly DependencyProperty SOTF_DisableDeathSPectatorProperty = DependencyProperty.Register(nameof(SOTF_DisableDeathSPectator), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        public bool SOTF_DisableDeathSPectator
        {
            get { return (bool)GetValue(SOTF_DisableDeathSPectatorProperty); }
            set { SetValue(SOTF_DisableDeathSPectatorProperty, value); }
        }

        public static readonly DependencyProperty SOTF_OnlyAdminRejoinAsSpectatorProperty = DependencyProperty.Register(nameof(SOTF_OnlyAdminRejoinAsSpectator), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        public bool SOTF_OnlyAdminRejoinAsSpectator
        {
            get { return (bool)GetValue(SOTF_OnlyAdminRejoinAsSpectatorProperty); }
            set { SetValue(SOTF_OnlyAdminRejoinAsSpectatorProperty, value); }
        }

        public static readonly DependencyProperty SOTF_GamePlayLoggingProperty = DependencyProperty.Register(nameof(SOTF_GamePlayLogging), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        public bool SOTF_GamePlayLogging
        {
            get { return (bool)GetValue(SOTF_GamePlayLoggingProperty); }
            set { SetValue(SOTF_GamePlayLoggingProperty, value); }
        }

        public static readonly DependencyProperty SOTF_OutputGameReportProperty = DependencyProperty.Register(nameof(SOTF_OutputGameReport), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        public bool SOTF_OutputGameReport
        {
            get { return (bool)GetValue(SOTF_OutputGameReportProperty); }
            set { SetValue(SOTF_OutputGameReportProperty, value); }
        }

        public static readonly DependencyProperty SOTF_MaxNumberOfPlayersInTribeProperty = DependencyProperty.Register(nameof(SOTF_MaxNumberOfPlayersInTribe), typeof(int), typeof(ServerProfile), new PropertyMetadata(2));
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings, Key = "MaxNumberOfPlayersInTribe", ConditionedOn = nameof(SOTF_Enabled))]
        public int SOTF_MaxNumberOfPlayersInTribe
        {
            get { return (int)GetValue(SOTF_MaxNumberOfPlayersInTribeProperty); }
            set { SetValue(SOTF_MaxNumberOfPlayersInTribeProperty, value); }
        }

        public static readonly DependencyProperty SOTF_BattleNumOfTribesToStartGameProperty = DependencyProperty.Register(nameof(SOTF_BattleNumOfTribesToStartGame), typeof(int), typeof(ServerProfile), new PropertyMetadata(15));
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings, Key = "BattleNumOfTribesToStartGame", ConditionedOn = nameof(SOTF_Enabled))]
        public int SOTF_BattleNumOfTribesToStartGame
        {
            get { return (int)GetValue(SOTF_BattleNumOfTribesToStartGameProperty); }
            set { SetValue(SOTF_BattleNumOfTribesToStartGameProperty, value); }
        }

        public static readonly DependencyProperty SOTF_TimeToCollapseRODProperty = DependencyProperty.Register(nameof(SOTF_TimeToCollapseROD), typeof(int), typeof(ServerProfile), new PropertyMetadata(9000));
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings, Key = "TimeToCollapseROD", ConditionedOn = nameof(SOTF_Enabled))]
        public int SOTF_TimeToCollapseROD
        {
            get { return (int)GetValue(SOTF_TimeToCollapseRODProperty); }
            set { SetValue(SOTF_TimeToCollapseRODProperty, value); }
        }

        public static readonly DependencyProperty SOTF_BattleAutoStartGameIntervalProperty = DependencyProperty.Register(nameof(SOTF_BattleAutoStartGameInterval), typeof(int), typeof(ServerProfile), new PropertyMetadata(60));
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings, Key = "BattleAutoStartGameInterval", ConditionedOn = nameof(SOTF_Enabled))]
        public int SOTF_BattleAutoStartGameInterval
        {
            get { return (int)GetValue(SOTF_BattleAutoStartGameIntervalProperty); }
            set { SetValue(SOTF_BattleAutoStartGameIntervalProperty, value); }
        }

        public static readonly DependencyProperty SOTF_BattleAutoRestartGameIntervalProperty = DependencyProperty.Register(nameof(SOTF_BattleAutoRestartGameInterval), typeof(int), typeof(ServerProfile), new PropertyMetadata(45));
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings, Key = "BattleAutoRestartGameInterval", ConditionedOn = nameof(SOTF_Enabled))]
        public int SOTF_BattleAutoRestartGameInterval
        {
            get { return (int)GetValue(SOTF_BattleAutoRestartGameIntervalProperty); }
            set { SetValue(SOTF_BattleAutoRestartGameIntervalProperty, value); }
        }

        public static readonly DependencyProperty SOTF_BattleSuddenDeathIntervalProperty = DependencyProperty.Register(nameof(SOTF_BattleSuddenDeathInterval), typeof(int), typeof(ServerProfile), new PropertyMetadata(300));
        [IniFileEntry(IniFiles.GameUserSettings, IniFileSections.ServerSettings, Key = "BattleSuddenDeathInterval", ConditionedOn = nameof(SOTF_Enabled))]
        public int SOTF_BattleSuddenDeathInterval
        {
            get { return (int)GetValue(SOTF_BattleSuddenDeathIntervalProperty); }
            set { SetValue(SOTF_BattleSuddenDeathIntervalProperty, value); }
        }

        public static readonly DependencyProperty SOTF_NoEventsProperty = DependencyProperty.Register(nameof(SOTF_NoEvents), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        public bool SOTF_NoEvents
        {
            get { return (bool)GetValue(SOTF_NoEventsProperty); }
            set { SetValue(SOTF_NoEventsProperty, value); }
        }

        public static readonly DependencyProperty SOTF_NoBossesProperty = DependencyProperty.Register(nameof(SOTF_NoBosses), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        public bool SOTF_NoBosses
        {
            get { return (bool)GetValue(SOTF_NoBossesProperty); }
            set { SetValue(SOTF_NoBossesProperty, value); }
        }

        public static readonly DependencyProperty SOTF_BothBossesProperty = DependencyProperty.Register(nameof(SOTF_BothBosses), typeof(bool), typeof(ServerProfile), new PropertyMetadata(false));
        public bool SOTF_BothBosses
        {
            get { return (bool)GetValue(SOTF_BothBossesProperty); }
            set { SetValue(SOTF_BothBossesProperty, value); }
        }

        public static readonly DependencyProperty SOTF_EvoEventIntervalProperty = DependencyProperty.Register(nameof(SOTF_EvoEventInterval), typeof(float), typeof(ServerProfile), new PropertyMetadata(1.0f));
        public float SOTF_EvoEventInterval
        {
            get { return (float)GetValue(SOTF_EvoEventIntervalProperty); }
            set { SetValue(SOTF_EvoEventIntervalProperty, value); }
        }

        public static readonly DependencyProperty SOTF_RingStartTimeProperty = DependencyProperty.Register(nameof(SOTF_RingStartTime), typeof(float), typeof(ServerProfile), new PropertyMetadata(1000.0f));
        public float SOTF_RingStartTime
        {
            get { return (float)GetValue(SOTF_RingStartTimeProperty); }
            set { SetValue(SOTF_RingStartTimeProperty, value); }
        }
        #endregion

        #region RCON
        public static readonly DependencyProperty RCONWindowExtentsProperty = DependencyProperty.Register(nameof(RCONWindowExtents), typeof(Rect), typeof(ServerProfile), new PropertyMetadata(new Rect(0f, 0f, 0f, 0f)));
        public Rect RCONWindowExtents
        {
            get { return (Rect)GetValue(RCONWindowExtentsProperty); }
            set { SetValue(RCONWindowExtentsProperty, value); }
        }
        #endregion

        //public static readonly DependencyProperty ConfigOverrideItemCraftingCostsProperty = DependencyProperty.Register(nameof(ConfigOverrideItemCraftingCosts), typeof(AggregateIniValueList<Crafting>), typeof(ServerProfile), new PropertyMetadata(null));
        //[XmlIgnore]
        //[IniFileEntry(IniFiles.Game, IniFileSections.GameMode)]
        //public AggregateIniValueList<Crafting> ConfigOverrideItemCraftingCosts
        //{
        //    get { return (AggregateIniValueList<Crafting>)GetValue(ConfigOverrideItemCraftingCostsProperty); }
        //    set { SetValue(ConfigOverrideItemCraftingCostsProperty, value); }
        //}
        #endregion

        #region Methods
        internal static ServerProfile FromDefaults()
        {
            var settings = new ServerProfile();
            settings.DinoSpawnWeightMultipliers.Reset();
            settings.TamedDinoClassResistanceMultipliers.Reset();
            settings.TamedDinoClassDamageMultipliers.Reset();
            settings.DinoClassResistanceMultipliers.Reset();
            settings.DinoClassDamageMultipliers.Reset();
            settings.HarvestResourceItemAmountClassMultipliers.Reset();
            settings.ResetLevelProgressionToOfficial(LevelProgression.Player);
            settings.ResetLevelProgressionToOfficial(LevelProgression.Dino);
            settings.PerLevelStatsMultiplier_DinoTamed.Reset();
            settings.PerLevelStatsMultiplier_DinoTamed_Add.Reset();
            settings.PerLevelStatsMultiplier_DinoTamed_Affinity.Reset();
            settings.PerLevelStatsMultiplier_DinoWild.Reset();
            settings.PerLevelStatsMultiplier_Player.Reset();
            return settings;
        }

        private void GetDefaultDirectories()
        {
            if (String.IsNullOrWhiteSpace(InstallDirectory))
            {
                InstallDirectory = Path.IsPathRooted(Config.Default.ServersInstallDir) ? Path.Combine(Config.Default.ServersInstallDir)
                                                                                       : Path.Combine(Config.Default.DataDir, Config.Default.ServersInstallDir);
            }
        }

        private LevelList GetLevelList(LevelProgression levelProgression)
        {
            LevelList list = null;
            switch (levelProgression)
            {
                case LevelProgression.Player:
                    list = this.PlayerLevels;
                    break;

                case LevelProgression.Dino:
                    list = this.DinoLevels;
                    break;

                default:
                    throw new ArgumentException("Invalid level progression type specified.");
            }
            return list;
        }

        public string GetLauncherFile()
        {
            return Path.Combine(this.InstallDirectory, Config.Default.ServerConfigRelativePath, Config.Default.LauncherFile);
        }

        public string GetProfileIniDir()
        {
            return Path.Combine(Path.GetDirectoryName(GetProfileFile()), this.ProfileName);
        }

        public string GetProfileKey()
        {
            return TaskSchedulerUtils.ComputeKey(this.InstallDirectory);
        }

        public string GetProfileFile()
        {
            return Path.Combine(Config.Default.ConfigDirectory, Path.ChangeExtension(this.ProfileName, Config.Default.ProfileExtension));
        }

        public string GetServerAppId()
        {
            try
            {
                var appFile = Path.Combine(InstallDirectory, Config.Default.ServerBinaryRelativePath, Config.Default.ServerAppIdFile);
                return File.Exists(appFile) ? File.ReadAllText(appFile).Trim() : string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        public string GetServerExeFile()
        {
            return Path.Combine(this.InstallDirectory, Config.Default.ServerBinaryRelativePath, Config.Default.ServerExe);
        }

        public string GetServerArgs()
        {
            var serverArgs = new StringBuilder();

            serverArgs.Append(this.ServerMap);

            serverArgs.Append("?listen");

            // These are used to match the server to the profile.
            if (!String.IsNullOrWhiteSpace(this.ServerIP))
            {
                serverArgs.Append("?MultiHome=").Append(this.ServerIP);
            }
            serverArgs.Append("?Port=").Append(this.ServerConnectionPort);
            serverArgs.Append("?QueryPort=").Append(this.ServerPort);
            serverArgs.Append("?MaxPlayers=").Append(this.MaxPlayers);

            if (this.UseRawSockets)
            {
                serverArgs.Append("?bRawSockets");
            }

            if (this.SOTF_Enabled)
            {
                serverArgs.Append("?EvoEventInterval=").Append(this.SOTF_EvoEventInterval);
                serverArgs.Append("?RingStartTime=").Append(this.SOTF_RingStartTime);
            }

            if (!String.IsNullOrWhiteSpace(this.AdditionalArgs))
            {
                var addArgs = this.AdditionalArgs.TrimStart();
                if (!addArgs.StartsWith("?"))
                    serverArgs.Append(" ");
                serverArgs.Append(addArgs);
            }

            if (!string.IsNullOrWhiteSpace(this.TotalConversionModId))
            {
                serverArgs.Append($" -TotalConversionMod={this.TotalConversionModId}");
            }

            if (this.EnableAllowCaveFlyers)
            {
                serverArgs.Append(" -ForceAllowCaveFlyers");
            }

            if (this.EnableAutoDestroyStructures)
            {
                serverArgs.Append(" -AutoDestroyStructures");
            }

            if (this.EnableNoFishLoot)
            {
                serverArgs.Append(" -nofishloot");
            }

            if(this.SOTF_Enabled)
            {
                if (this.SOTF_OutputGameReport)
                {
                    serverArgs.Append(" -OutputGameReport");
                }

                if (this.SOTF_GamePlayLogging)
                {
                    serverArgs.Append(" -gameplaylogging");
                }

                if (this.SOTF_DisableDeathSPectator)
                {
                    serverArgs.Append(" -DisableDeathSpectator");
                }

                if(this.SOTF_OnlyAdminRejoinAsSpectator)
                {
                    serverArgs.Append(" -OnlyAdminRejoinAsSpectator");
                }

                if (this.SOTF_NoEvents)
                {
                    serverArgs.Append(" -noevents");
                }

                if (this.SOTF_NoBosses)
                {
                    serverArgs.Append(" -nobosses");
                }
                else if (this.SOTF_BothBosses)
                {
                    serverArgs.Append(" -bothbosses");
                }
            }

            if (this.UseBattlEye)
            {
                serverArgs.Append(" -UseBattlEye");
            }

            if (this.DisableValveAntiCheatSystem)
            {
                serverArgs.Append(" -insecure");
            }

            if (this.DisableAntiSpeedHackDetection || this.SpeedHackBias == 0.0f)
            {
                serverArgs.Append(" -noantispeedhack");
            }
            else if (this.SpeedHackBias != 1.0f)
            {
                serverArgs.Append($" -speedhackbias={this.SpeedHackBias}f");
            }

            if (this.DisablePlayerMovePhysicsOptimization)
            {
                serverArgs.Append(" -nocombineclientmoves");
            }

            if (this.ForceRespawnDinos)
            {
                serverArgs.Append(" -forcerespawndinos");
            }

            if (this.EnableServerAdminLogs)
            {
                serverArgs.Append(" -servergamelog");
            }

            if (this.ForceDirectX10)
            {
                serverArgs.Append(" -d3d10");
            }

            if (this.ForceShaderModel4)
            {
                serverArgs.Append(" -sm4");
            }

            if (this.ForceLowMemory)
            {
                serverArgs.Append(" -lowmemory");
            }

            if (this.ForceNoManSky)
            {
                serverArgs.Append(" -nomansky");
            }

            if (this.UseAllAvailableCores)
            {
                serverArgs.Append(" -useallavailablecores");
            }

            if (this.UseCache)
            {
                serverArgs.Append(" -usecache");
            }

            if (this.EnableWebAlarm)
            {
                serverArgs.Append(" -webalarm");
            }

            if (this.AutoManagedMods)
            {
                serverArgs.Append(" -automanagedmods");
            }

            if (this.UseOldSaveFormat)
            {
                serverArgs.Append(" -oldsaveformat");
            }

            if (this.UseNoMemoryBias)
            {
                serverArgs.Append(" -nomemorybias");
            }

            serverArgs.Append(' ');
            serverArgs.Append(Config.Default.ServerCommandLineStandardArgs);

            return serverArgs.ToString();
        }

        public static ServerProfile LoadFrom(string path)
        {
            ServerProfile settings = null;
            if (Path.GetExtension(path) == Config.Default.ProfileExtension)
            {
                XmlSerializer serializer = new XmlSerializer(typeof(ServerProfile));

                using (var reader = File.OpenRead(path))
                {
                    settings = (ServerProfile)serializer.Deserialize(reader);
                    settings.IsDirty = false;
                }

                var profileIniPath = Path.Combine(Path.ChangeExtension(path, null), Config.Default.ServerGameUserSettingsFile);
                var configIniPath = Path.Combine(settings.InstallDirectory, Config.Default.ServerConfigRelativePath, Config.Default.ServerGameUserSettingsFile);
                if (File.Exists(configIniPath))
                {
                    settings = LoadFromINIFiles(configIniPath, settings);
                }
                else if (File.Exists(profileIniPath))
                {
                    settings = LoadFromINIFiles(profileIniPath, settings);
                }
            }
            else
            {
                settings = LoadFromINIFiles(path, settings);
                settings.InstallDirectory = Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(path)))));
            }

            if (settings.PlayerLevels.Count == 0)
            {
                settings.ResetLevelProgressionToOfficial(LevelProgression.Player);
                settings.ResetLevelProgressionToOfficial(LevelProgression.Dino);
                settings.EnableLevelProgressions = false;
            }

            //
            // Since these are not inserted the normal way, we force a recomputation here.
            //
            settings.PlayerLevels.UpdateTotals();
            settings.DinoLevels.UpdateTotals();
            settings.DinoSettings.RenderToView();
            settings._lastSaveLocation = path;
            return settings;
        }

        private static ServerProfile LoadFromINIFiles(string path, ServerProfile settings)
        {
            var iniFile = new SystemIniFile(Path.GetDirectoryName(path));
            settings = settings ?? new ServerProfile();
            iniFile.Deserialize(settings);

            var values = iniFile.ReadSection(IniFiles.Game, IniFileSections.GameMode);

            var levelRampOverrides = values.Where(s => s.StartsWith("LevelExperienceRampOverrides=")).ToArray();
            if (levelRampOverrides.Length > 0)
            {
                var engramPointOverrides = values.Where(s => s.StartsWith("OverridePlayerLevelEngramPoints="));

                settings.EnableLevelProgressions = true;
                settings.PlayerLevels = LevelList.FromINIValues(levelRampOverrides[0], engramPointOverrides);

                if (levelRampOverrides.Length > 1)
                {
                    settings.DinoLevels = LevelList.FromINIValues(levelRampOverrides[1], null);
                }
            }

            return settings;
        }

        public static ServerProfile LoadFromProfileFile(string path)
        {
            ServerProfile settings = null;
            if (Path.GetExtension(path) == Config.Default.ProfileExtension)
            {
                XmlSerializer serializer = new XmlSerializer(typeof(ServerProfile));

                using (var reader = File.OpenRead(path))
                {
                    settings = (ServerProfile)serializer.Deserialize(reader);
                    settings.IsDirty = false;
                }

                var profileIniPath = Path.Combine(Path.ChangeExtension(path, null), Config.Default.ServerGameUserSettingsFile);
                var configIniPath = Path.Combine(settings.InstallDirectory, Config.Default.ServerConfigRelativePath, Config.Default.ServerGameUserSettingsFile);
                if (File.Exists(configIniPath))
                {
                    settings = LoadFromINIFiles(configIniPath, settings);
                }
                else if (File.Exists(profileIniPath))
                {
                    settings = LoadFromINIFiles(profileIniPath, settings);
                }

                if (settings.PlayerLevels.Count == 0)
                {
                    settings.ResetLevelProgressionToOfficial(LevelProgression.Player);
                    settings.ResetLevelProgressionToOfficial(LevelProgression.Dino);
                    settings.EnableLevelProgressions = false;
                }

                //
                // Since these are not inserted the normal way, we force a recomputation here.
                //
                settings.PlayerLevels.UpdateTotals();
                settings.DinoLevels.UpdateTotals();
                settings.DinoSettings.RenderToView();
                settings._lastSaveLocation = path;
            }
            return settings;
        }

        public void Save(bool updateSchedules)
        {
            // ensure that the auto settings are switched off for SotF servers
            if (SOTF_Enabled)
            {
                EnableAutoRestart = false;
                EnableAutoUpdate = false;
                AutoRestartIfShutdown = false;
            }

            // ensure that the ARK mod management is switched off for ASM controlled profiles
            if (EnableAutoUpdate)
                AutoManagedMods = false;

            // ensure that the MAX XP settings for player and dinos are set to the last custom level
            if (EnableLevelProgressions)
            {
                // dinos
                var list = GetLevelList(LevelProgression.Dino);
                var lastxp = (list == null || list.Count == 0) ? 0 : list[list.Count - 1].XPRequired;

                if (lastxp > OverrideMaxExperiencePointsDino)
                    OverrideMaxExperiencePointsDino = lastxp;

                // players
                list = GetLevelList(LevelProgression.Player);
                lastxp = (list == null || list.Count == 0) ? 0 : list[list.Count - 1].XPRequired;

                if (lastxp > OverrideMaxExperiencePointsPlayer)
                    OverrideMaxExperiencePointsPlayer = lastxp;
            }

            this.DinoSettings.RenderToModel();

            //
            // Save the profile
            //
            SaveProfile();

            //
            // Write the INI files
            //
            SaveINIFiles();

            //
            // If this was a rename, remove the old profile after writing the new one.
            //
            if(!String.Equals(GetProfileFile(), this._lastSaveLocation))
            {
                try
                {
                    if (File.Exists(this._lastSaveLocation))
                    {
                        File.Delete(this._lastSaveLocation);
                    }

                    var iniDir = Path.ChangeExtension(this._lastSaveLocation, null);
                    if (Directory.Exists(iniDir))
                    {
                        Directory.Delete(iniDir, recursive: true);
                    }
                }
                catch(IOException)
                {
                    // We tried...
                }

                this._lastSaveLocation = GetProfileFile();
            }

            SaveLauncher();
            UpdateWebAlarm();

            if (updateSchedules)
            {
                UpdateSchedules();
            }
        }

        public void SaveProfile()
        {
            //
            // Save the profile
            //
            var serializer = new XmlSerializer(this.GetType());
            using (var stream = File.Open(GetProfileFile(), FileMode.Create))
            {
                serializer.Serialize(stream, this);
            }
        }

        private void SaveLauncher()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(GetLauncherFile()));
            File.WriteAllText(GetLauncherFile(), $"start \"{ProfileName}\" /normal \"{GetServerExeFile()}\" {GetServerArgs()}");
        }

        public void SaveINIFiles()
        {
            //
            // Save alongside the .profile
            //
            string profileIniDir = GetProfileIniDir();
            Directory.CreateDirectory(profileIniDir);
            SaveINIFile(profileIniDir);

            //
            // Save to the installation location
            //
            string configDir = Path.Combine(this.InstallDirectory, Config.Default.ServerConfigRelativePath);
            Directory.CreateDirectory(configDir);
            SaveINIFile(configDir);
        }

        private void SaveINIFile(string profileIniDir)
        {
            var iniFile = new SystemIniFile(profileIniDir);
            iniFile.Serialize(this);

            var values = iniFile.ReadSection(IniFiles.Game, IniFileSections.GameMode);

            var filteredValues = values.Where(s => !s.StartsWith("LevelExperienceRampOverrides=") && !s.StartsWith("OverridePlayerLevelEngramPoints=")).ToList();
            if (this.EnableLevelProgressions)
            {
                //
                // These must be added in this order: Player, then Dinos, per the ARK INI file format.
                //
                filteredValues.Add(this.PlayerLevels.ToINIValueForXP());
                filteredValues.Add(this.DinoLevels.ToINIValueForXP());
                filteredValues.AddRange(this.PlayerLevels.ToINIValuesForEngramPoints());
            }

            iniFile.WriteSection(IniFiles.Game, IniFileSections.GameMode, filteredValues.ToArray());
        }

        public bool UpdateSchedules()
        {
            SaveLauncher();

            if (!SecurityUtils.IsAdministrator())
                return true;

            if (!SecurityUtils.SetDirectoryOwnershipForAllUsers(this.InstallDirectory))
            {
               _logger.Error($"Unable to set directory permissions for {this.InstallDirectory}.");
                return false;
            }

            var taskKey = GetProfileKey();

            // remove the old task schedule
            TaskSchedulerUtils.ScheduleUpdates(taskKey, 0, Config.Default.AutoUpdate_CacheDir, this.InstallDirectory, null, 0, null, 0, null);

            if(!TaskSchedulerUtils.ScheduleAutoStart(taskKey, this.EnableAutoStart, GetLauncherFile(), String.Empty))
            {
                return false;
            }

            TimeSpan restartTime;
            var command = Assembly.GetEntryAssembly().Location;
            if (!TaskSchedulerUtils.ScheduleAutoRestart(taskKey, command, this.EnableAutoRestart ? (TimeSpan.TryParseExact(this.AutoRestartTime, "g", null, out restartTime) ? restartTime : (TimeSpan?)null) : null))
            {
                return false;
            }

            return true;
        }

        private void UpdateWebAlarm()
        {
            var alarmPostCredentialsFile = Path.Combine(this.InstallDirectory, Config.Default.SavedRelativePath, Config.Default.WebAlarmFile);

            try
            {
                // check if the web alarm option is enabled.
                if (this.EnableWebAlarm)
                {
                    // check if the directory exists.
                    if (!Directory.Exists(Path.GetDirectoryName(alarmPostCredentialsFile)))
                        Directory.CreateDirectory(Path.GetDirectoryName(alarmPostCredentialsFile));

                    var contents = new StringBuilder();
                    contents.AppendLine($"{this.WebAlarmKey}");
                    contents.AppendLine($"{this.WebAlarmUrl}");
                    File.WriteAllText(alarmPostCredentialsFile, contents.ToString());
                }
                else
                {
                    // check if the files exists and delete it.
                    if (File.Exists(alarmPostCredentialsFile))
                        File.Delete(alarmPostCredentialsFile);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Web Alarm Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public bool Validate(out string validationMessage)
        {
            validationMessage = string.Empty;
            StringBuilder result = new StringBuilder();

            var appId = SOTF_Enabled ? Config.Default.AppId_SotF : Config.Default.AppId;

            if (Config.Default.ValidateProfileOnServerStart && !AutoManagedMods)
            {
                // build a list of mods to be processed
                var serverMapModId = ModUtils.GetMapModId(ServerMap);
                var serverMapName = ModUtils.GetMapName(ServerMap);
                var modIds = ModUtils.GetModIdList(ServerModIds);
                modIds = ModUtils.ValidateModList(modIds);

                var modIdList = new List<string>();
                if (!string.IsNullOrWhiteSpace(serverMapModId))
                    modIdList.Add(serverMapModId);
                if (!string.IsNullOrWhiteSpace(TotalConversionModId))
                    modIdList.Add(TotalConversionModId);
                modIdList.AddRange(modIds);

                modIdList = ModUtils.ValidateModList(modIdList);

                var modDetails = ModUtils.GetSteamModDetails(modIdList);

                // check for map name.
                if (string.IsNullOrWhiteSpace(ServerMap))
                    result.AppendLine("The map name has not been entered.");

                // check if the server executable exists
                var serverFolder = Path.Combine(InstallDirectory, Config.Default.ServerBinaryRelativePath);
                var serverFile = Path.Combine(serverFolder, Config.Default.ServerExe);
                if (!Directory.Exists(serverFolder))
                    result.AppendLine("Server files have not been downloaded, server folder does not exist.");
                else if (!File.Exists(serverFile))
                    result.AppendLine($"Server files have not been downloaded properly, server executable file ({Config.Default.ServerExe}) does not exist.");
                else
                {
                    var serverAppId = GetServerAppId();
                    if (!serverAppId.Equals(appId))
                        result.AppendLine("The server files are for a different Ark application.");
                }

                // check if the map is a mod and confirm the map name.
                if (!string.IsNullOrWhiteSpace(serverMapModId))
                {
                    var modFolder = ModUtils.GetModPath(InstallDirectory, serverMapModId);
                    if (!Directory.Exists(modFolder))
                        result.AppendLine("Map mod has not been downloaded, mod folder does not exist.");
                    else if (!File.Exists($"{modFolder}.mod"))
                        result.AppendLine("Map mod has not been downloaded properly, mod file does not exist.");
                    else
                    {
                        var modType = ModUtils.GetModType(InstallDirectory, serverMapModId);
                        if (modType == ModUtils.MODTYPE_UNKNOWN)
                            result.AppendLine("Map mod has not been downloaded properly, mod file is invalid.");
                        else if (modType != ModUtils.MODTYPE_MAP)
                            result.AppendLine("The map mod is not a valid map mod.");
                        else
                        {
                            // do not process any mods that are not included in the mod list.
                            if (modIdList.Contains(serverMapModId))
                            {
                                var mapName = ModUtils.GetMapName(InstallDirectory, serverMapModId);
                                if (string.IsNullOrWhiteSpace(mapName))
                                    result.AppendLine("Map mod file does not exist or is invalid.");
                                else if (!mapName.Equals(serverMapName))
                                    result.AppendLine("The map name does not match the map mod's map name.");
                                else
                                {
                                    var modDetail = modDetails?.publishedfiledetails?.FirstOrDefault(d => d.publishedfileid.Equals(TotalConversionModId));
                                    if (modDetail != null)
                                    {
                                        if (!modDetail.creator_app_id.Equals(appId))
                                            result.AppendLine("The map mod is for a different Ark application.");
                                        else
                                        {
                                            var modVersion = ModUtils.GetModLatestTime(ModUtils.GetLatestModTimeFile(InstallDirectory, TotalConversionModId));
                                            if (!modVersion.Equals(modDetail.time_updated))
                                                result.AppendLine("The map mod is outdated.");
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                // check for a total conversion mod
                if (!string.IsNullOrWhiteSpace(TotalConversionModId))
                {
                    var modFolder = ModUtils.GetModPath(InstallDirectory, TotalConversionModId);
                    if (!Directory.Exists(modFolder))
                        result.AppendLine("Total conversion mod has not been downloaded, mod folder does not exist.");
                    else if (!File.Exists($"{modFolder}.mod"))
                        result.AppendLine("Total conversion mod has not been downloaded properly, mod file does not exist.");
                    else
                    {
                        var modType = ModUtils.GetModType(InstallDirectory, TotalConversionModId);
                        if (modType == ModUtils.MODTYPE_UNKNOWN)
                            result.AppendLine("Total conversion mod has not been downloaded properly, mod file is invalid.");
                        else if (modType != ModUtils.MODTYPE_TOTCONV)
                            result.AppendLine("The total conversion mod is not a valid total conversion mod.");
                        else
                        {
                            // do not process any mods that are not included in the mod list.
                            if (modIdList.Contains(TotalConversionModId))
                            {
                                var mapName = ModUtils.GetMapName(InstallDirectory, TotalConversionModId);
                                if (string.IsNullOrWhiteSpace(mapName))
                                    result.AppendLine("Total conversion mod file does not exist or is invalid.");
                                else if (!mapName.Equals(serverMapName))
                                    result.AppendLine("The map name does not match the total conversion mod's map name.");
                                else
                                {
                                    var modDetail = modDetails?.publishedfiledetails?.FirstOrDefault(d => d.publishedfileid.Equals(TotalConversionModId));
                                    if (modDetail != null)
                                    {
                                        if (!modDetail.creator_app_id.Equals(appId))
                                            result.AppendLine("The total conversion mod is for a different Ark application.");
                                        else
                                        {
                                            var modVersion = ModUtils.GetModLatestTime(ModUtils.GetLatestModTimeFile(InstallDirectory, TotalConversionModId));
                                            if (!modVersion.Equals(modDetail.time_updated))
                                                result.AppendLine("The total conversion mod is outdated.");
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                // check for the mods
                foreach (var modId in modIds)
                {
                    var modFolder = ModUtils.GetModPath(InstallDirectory, modId);
                    if (!Directory.Exists(modFolder))
                        result.AppendLine($"Mod {modId} has not been downloaded, mod folder does not exist.");
                    else if (!File.Exists($"{modFolder}.mod"))
                        result.AppendLine($"Mod {modId} has not been downloaded properly, mod file does not exist.");
                    else
                    {
                        var modDetail = modDetails?.publishedfiledetails?.FirstOrDefault(d => d.publishedfileid.Equals(modId));
                        if (modDetail != null)
                        {
                            if (!modDetail.creator_app_id.Equals(appId))
                                result.AppendLine($"Mod {modId} is for a different Ark application.");
                            else
                            {
                                var modVersion = ModUtils.GetModLatestTime(ModUtils.GetLatestModTimeFile(InstallDirectory, modId));
                                if (modVersion == 0 || !modVersion.Equals(modDetail.time_updated))
                                    result.AppendLine($"Mod {modId} is outdated.");
                            }
                        }
                    }
                }
            }

            validationMessage = result.ToString();
            return string.IsNullOrWhiteSpace(validationMessage);
        }

        public string ToOutputString()
        {
            //
            // serializes the profile to a string
            //
            var result = new StringBuilder();
            var serializer = new XmlSerializer(this.GetType());
            using (var stream = new StringWriter(result))
            {
                serializer.Serialize(stream, this);
            }
            return result.ToString();
        }

        #region Export Methods
        public void ExportDinoLevels(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return;

            LevelList list = GetLevelList(LevelProgression.Dino);

            StringBuilder output = new StringBuilder();
            foreach (var level in list)
            {
                output.AppendLine($"{level.LevelIndex}{CSV_DELIMITER}{level.XPRequired}");
            }

            File.WriteAllText(fileName, output.ToString());
        }
        public void ExportPlayerLevels(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return;

            LevelList list = GetLevelList(LevelProgression.Player);

            StringBuilder output = new StringBuilder();
            foreach (var level in list)
            {
                output.AppendLine($"{level.LevelIndex}{CSV_DELIMITER}{level.XPRequired}{CSV_DELIMITER}{level.EngramPoints}");
            }

            File.WriteAllText(fileName, output.ToString());
        }
        #endregion

        #region Import Methods
        public void ImportDinoLevels(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return;
            if (!File.Exists(fileName))
                return;

            CsvParserOptions csvParserOptions = new CsvParserOptions(false, new[] { CSV_DELIMITER });
            CsvDinoLevelMapping csvMapper = new CsvDinoLevelMapping();
            CsvParser<ImportLevel> csvParser = new CsvParser<ImportLevel>(csvParserOptions, csvMapper);

            var result = csvParser.ReadFromFile(fileName, Encoding.ASCII).ToList();
            if (result.Any(r => !r.IsValid))
            {
                var error = result.First(r => r.Error != null);
                throw new Exception($"Import error occured in column {error.Error.ColumnIndex} with a value of {error.Error.Value}");
            }

            LevelList list = GetLevelList(LevelProgression.Dino);
            list.Clear();

            foreach (var level in result)
            {
                list.Add(level.Result.AsLevel());
            }

            list.UpdateTotals();
        }

        public void ImportPlayerLevels(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return;
            if (!File.Exists(fileName))
                return;

            CsvParserOptions csvParserOptions = new CsvParserOptions(false, new[] { CSV_DELIMITER });
            CsvPlayerLevelMapping csvMapper = new CsvPlayerLevelMapping();
            CsvParser<ImportLevel> csvParser = new CsvParser<ImportLevel>(csvParserOptions, csvMapper);

            var result = csvParser.ReadFromFile(fileName, Encoding.ASCII).ToList();
            if (result.Any(r => !r.IsValid))
            {
                var error = result.First(r => r.Error != null);
                throw new Exception($"Import error occured in column {error.Error.ColumnIndex} with a value of {error.Error.Value}");
            }

            LevelList list = GetLevelList(LevelProgression.Player);
            list.Clear();

            foreach (var level in result)
            {
                list.Add(level.Result.AsLevel());
            }

            list.UpdateTotals();
        }
        #endregion

        #region Reset Methods
        public void ClearLevelProgression(LevelProgression levelProgression)
        {
            var list = GetLevelList(levelProgression);
            list.Clear();
            list.Add(new Level { LevelIndex = 0, XPRequired = 1, EngramPoints = 0 });
            list.UpdateTotals();
        }

        public void ResetLevelProgressionToOfficial(LevelProgression levelProgression)
        {
            LevelList list = GetLevelList(levelProgression);

            list.Clear();

            switch (levelProgression)
            {
                case LevelProgression.Player:
                    list.AddRange(GameData.LevelProgressionPlayerOfficial);
                    break;
                case LevelProgression.Dino:
                    list.AddRange(GameData.LevelProgressionDinoOfficial);
                    break;
            }
        }

        // individual value reset methods
        public void ResetMapName(string mapName)
        {
            this.ServerMap = mapName;
        }

        public void ResetOverrideMaxExperiencePointsPlayer()
        {
            this.ClearValue(OverrideMaxExperiencePointsPlayerProperty);
        }

        public void ResetOverrideMaxExperiencePointsDino()
        {
            this.ClearValue(OverrideMaxExperiencePointsDinoProperty);
        }

        public void ResetRCONWindowExtents()
        {
            this.ClearValue(RCONWindowExtentsProperty);
        }

        // section reset methods
        public void ResetAdministrationSection()
        {
            this.ClearValue(ServerNameProperty);
            this.ClearValue(ServerPasswordProperty);
            this.ClearValue(AdminPasswordProperty);
            this.ClearValue(SpectatorPasswordProperty);

            this.ClearValue(ServerConnectionPortProperty);
            this.ClearValue(ServerPortProperty);
            this.ClearValue(ServerIPProperty);
            this.ClearValue(UseRawSocketsProperty);

            this.ClearValue(EnableBanListURLProperty);
            this.ClearValue(BanListURLProperty);
            this.ClearValue(MaxPlayersProperty);
            this.ClearValue(EnableKickIdlePlayersProperty);
            this.ClearValue(KickIdlePlayersPeriodProperty);

            this.ClearValue(RCONEnabledProperty);
            this.ClearValue(RCONPortProperty);
            this.ClearValue(RCONServerGameLogBufferProperty);
            this.ClearValue(AdminLoggingProperty);

            this.ClearValue(ServerMapProperty);
            this.ClearValue(TotalConversionModIdProperty);
            this.ClearValue(ServerModIdsProperty);
            this.ClearValue(AutoManagedModsProperty);

            this.ClearValue(EnableExtinctionEventProperty);
            this.ClearValue(ExtinctionEventTimeIntervalProperty);
            this.ClearValue(ExtinctionEventUTCProperty);

            this.ClearValue(AutoSavePeriodMinutesProperty);

            this.ClearValue(MOTDProperty);
            this.ClearValue(MOTDDurationProperty);

            this.ClearValue(DisableValveAntiCheatSystemProperty);
            this.ClearValue(DisablePlayerMovePhysicsOptimizationProperty);
            this.ClearValue(DisableAntiSpeedHackDetectionProperty);
            this.ClearValue(SpeedHackBiasProperty);
            this.ClearValue(UseBattlEyeProperty);
            this.ClearValue(MaxTribeLogsProperty);
            this.ClearValue(EnableServerAdminLogsProperty);
            this.ClearValue(ForceRespawnDinosProperty);
            this.ClearValue(ForceDirectX10Property);
            this.ClearValue(ForceShaderModel4Property);
            this.ClearValue(ForceLowMemoryProperty);
            this.ClearValue(ForceNoManSkyProperty);
            this.ClearValue(UseAllAvailableCoresProperty);
            this.ClearValue(UseCacheProperty);
            this.ClearValue(UseOldSaveFormatProperty);
            this.ClearValue(UseNoMemoryBiasProperty);

            this.ClearValue(EnableWebAlarmProperty);
            this.ClearValue(WebAlarmKeyProperty);
            this.ClearValue(WebAlarmUrlProperty);

            this.ClearValue(AdditionalArgsProperty);
        }

        public void ResetChatAndNotificationSection()
        {
            this.ClearValue(EnableGlobalVoiceChatProperty);
            this.ClearValue(EnableProximityChatProperty);
            this.ClearValue(EnablePlayerLeaveNotificationsProperty);
            this.ClearValue(EnablePlayerJoinedNotificationsProperty);
        }

        public void ResetCustomLevelsSection()
        {
            this.ClearValue(EnableLevelProgressionsProperty);

            this.PlayerLevels = new LevelList();
            this.ResetLevelProgressionToOfficial(LevelProgression.Player);

            this.DinoLevels = new LevelList();
            this.ResetLevelProgressionToOfficial(LevelProgression.Dino);
        }

        public void ResetDinoSettings()
        {
            this.ClearValue(OverrideMaxExperiencePointsDinoProperty);
            this.ClearValue(DinoDamageMultiplierProperty);
            this.ClearValue(TamedDinoDamageMultiplierProperty);
            this.ClearValue(DinoResistanceMultiplierProperty);
            this.ClearValue(TamedDinoResistanceMultiplierProperty);
            this.ClearValue(MaxTamedDinosProperty);
            this.ClearValue(DinoCharacterFoodDrainMultiplierProperty);
            this.ClearValue(DinoCharacterStaminaDrainMultiplierProperty);
            this.ClearValue(DinoCharacterHealthRecoveryMultiplierProperty);
            this.ClearValue(DinoCountMultiplierProperty);
            this.ClearValue(HarvestingDamageMultiplierDinoProperty);
            this.ClearValue(TurretDamageMultiplierDinoProperty);

            this.ClearValue(AllowRaidDinoFeedingProperty);
            this.ClearValue(RaidDinoCharacterFoodDrainMultiplierProperty);

            this.ClearValue(EnableAllowCaveFlyersProperty);
            this.ClearValue(EnableNoFishLootProperty);
            this.ClearValue(DisableDinoDecayPvEProperty);
            this.ClearValue(PvEDinoDecayPeriodMultiplierProperty);

            this.DinoSpawnWeightMultipliers = new AggregateIniValueList<DinoSpawn>(nameof(DinoSpawnWeightMultipliers), GameData.GetDinoSpawns);
            this.PreventDinoTameClassNames = new StringIniValueList(nameof(PreventDinoTameClassNames), () => new string[0]);
            this.NPCReplacements = new AggregateIniValueList<NPCReplacement>(nameof(NPCReplacements), GameData.GetNPCReplacements);
            this.TamedDinoClassDamageMultipliers = new AggregateIniValueList<ClassMultiplier>(nameof(TamedDinoClassDamageMultipliers), GameData.GetStandardDinoMultipliers);
            this.TamedDinoClassResistanceMultipliers = new AggregateIniValueList<ClassMultiplier>(nameof(TamedDinoClassResistanceMultipliers), GameData.GetStandardDinoMultipliers);
            this.DinoClassDamageMultipliers = new AggregateIniValueList<ClassMultiplier>(nameof(DinoClassDamageMultipliers), GameData.GetStandardDinoMultipliers);
            this.DinoClassResistanceMultipliers = new AggregateIniValueList<ClassMultiplier>(nameof(DinoClassResistanceMultipliers), GameData.GetStandardDinoMultipliers);
            this.DinoSettings = new DinoSettingsList(this.DinoSpawnWeightMultipliers, this.PreventDinoTameClassNames, this.NPCReplacements, this.TamedDinoClassDamageMultipliers, this.TamedDinoClassResistanceMultipliers, this.DinoClassDamageMultipliers, this.DinoClassResistanceMultipliers);

            this.PerLevelStatsMultiplier_DinoWild = new FloatIniValueArray(nameof(PerLevelStatsMultiplier_DinoWild), GameData.GetPerLevelStatsMultipliers_Default);
            this.PerLevelStatsMultiplier_DinoTamed = new FloatIniValueArray(nameof(PerLevelStatsMultiplier_DinoTamed), GameData.GetPerLevelStatsMultipliers_DinoTamed);
            this.PerLevelStatsMultiplier_DinoTamed_Add = new FloatIniValueArray(nameof(PerLevelStatsMultiplier_DinoTamed_Add), GameData.GetPerLevelStatsMultipliers_DinoTamed_Add);
            this.PerLevelStatsMultiplier_DinoTamed_Affinity = new FloatIniValueArray(nameof(PerLevelStatsMultiplier_DinoTamed_Affinity), GameData.GetPerLevelStatsMultipliers_DinoTamed_Affinity);

            this.ClearValue(MatingIntervalMultiplierProperty);
            this.ClearValue(EggHatchSpeedMultiplierProperty);
            this.ClearValue(BabyMatureSpeedMultiplierProperty);
            this.ClearValue(BabyFoodConsumptionSpeedMultiplierProperty);

            this.ClearValue(DisableImprintDinoBuffProperty);
            this.ClearValue(AllowAnyoneBabyImprintCuddleProperty);
            this.ClearValue(BabyImprintingStatScaleMultiplierProperty);
            this.ClearValue(BabyCuddleIntervalMultiplierProperty);
            this.ClearValue(BabyCuddleGracePeriodMultiplierProperty);
            this.ClearValue(BabyCuddleLoseImprintQualitySpeedMultiplierProperty);
        }

        public void ResetEngramsSection()
        {
            this.OverrideNamedEngramEntries = new AggregateIniValueList<EngramEntry>(nameof(OverrideNamedEngramEntries), GameData.GetStandardEngramOverrides);
            this.OverrideNamedEngramEntries.Reset();
        }

        public void ResetEnvironmentSection()
        {
            this.ClearValue(TamingSpeedMultiplierProperty);
            this.ClearValue(HarvestAmountMultiplierProperty);
            this.ClearValue(ResourcesRespawnPeriodMultiplierProperty);
            this.ClearValue(ResourceNoReplenishRadiusPlayersProperty);
            this.ClearValue(ResourceNoReplenishRadiusStructuresProperty);
            this.ClearValue(ClampResourceHarvestDamageProperty);
            this.ClearValue(HarvestHealthMultiplierProperty);

            this.HarvestResourceItemAmountClassMultipliers = new AggregateIniValueList<ResourceClassMultiplier>(nameof(HarvestResourceItemAmountClassMultipliers), GameData.GetStandardResourceMultipliers);
            this.HarvestResourceItemAmountClassMultipliers.Reset();

            this.ClearValue(DayCycleSpeedScaleProperty);
            this.ClearValue(DayTimeSpeedScaleProperty);
            this.ClearValue(NightTimeSpeedScaleProperty);
            this.ClearValue(GlobalSpoilingTimeMultiplierProperty);
            this.ClearValue(GlobalItemDecompositionTimeMultiplierProperty);
            this.ClearValue(GlobalCorpseDecompositionTimeMultiplierProperty);
            this.ClearValue(CropDecaySpeedMultiplierProperty);
            this.ClearValue(CropGrowthSpeedMultiplierProperty);
            this.ClearValue(LayEggIntervalMultiplierProperty);
            this.ClearValue(PoopIntervalMultiplierProperty);
        }

        public void ResetHUDAndVisualsSection()
        {
            this.ClearValue(AllowCrosshairProperty);
            this.ClearValue(AllowHUDProperty);
            this.ClearValue(AllowThirdPersonViewProperty);
            this.ClearValue(AllowMapPlayerLocationProperty);
            this.ClearValue(AllowPVPGammaProperty);
            this.ClearValue(AllowPvEGammaProperty);
            this.ClearValue(ShowFloatingDamageTextProperty);
            this.ClearValue(AllowHitMarkersProperty);
        }

        public void ResetPlayerSettings()
        {
            this.ClearValue(EnableFlyerCarryProperty);
            this.ClearValue(XPMultiplierProperty);
            this.ClearValue(OverrideMaxExperiencePointsPlayerProperty);
            this.ClearValue(PlayerDamageMultiplierProperty);
            this.ClearValue(PlayerResistanceMultiplierProperty);
            this.ClearValue(PlayerCharacterWaterDrainMultiplierProperty);
            this.ClearValue(PlayerCharacterFoodDrainMultiplierProperty);
            this.ClearValue(PlayerCharacterStaminaDrainMultiplierProperty);
            this.ClearValue(PlayerCharacterHealthRecoveryMultiplierProperty);
            this.ClearValue(HarvestingDamageMultiplierPlayerProperty);

            this.PerLevelStatsMultiplier_Player = new FloatIniValueArray(nameof(PerLevelStatsMultiplier_Player), GameData.GetPerLevelStatsMultipliers_Default);
        }

        public void ResetRulesSection()
        {
            this.ClearValue(EnableHardcoreProperty);
            this.ClearValue(EnablePVPProperty);
            this.ClearValue(AllowCaveBuildingPvEProperty);
            this.ClearValue(DisableFriendlyFirePvPProperty);
            this.ClearValue(DisableFriendlyFirePvEProperty);
            this.ClearValue(DisableLootCratesProperty);
            this.ClearValue(EnableExtraStructurePreventionVolumesProperty);

            this.ClearValue(DifficultyOffsetProperty);
            this.ClearValue(MaxNumberOfPlayersInTribeProperty);

            this.ClearValue(EnableTributeDownloadsProperty);
            this.ClearValue(PreventDownloadSurvivorsProperty);
            this.ClearValue(PreventDownloadItemsProperty);
            this.ClearValue(PreventDownloadDinosProperty);

            this.ClearValue(IncreasePvPRespawnIntervalProperty);
            this.ClearValue(IncreasePvPRespawnIntervalCheckPeriodProperty);
            this.ClearValue(IncreasePvPRespawnIntervalMultiplierProperty);
            this.ClearValue(IncreasePvPRespawnIntervalBaseAmountProperty);

            this.ClearValue(PreventOfflinePvPProperty);
            this.ClearValue(PreventOfflinePvPIntervalProperty);

            this.ClearValue(AutoPvETimerProperty);
            this.ClearValue(AutoPvEUseSystemTimeProperty);
            this.ClearValue(AutoPvEStartTimeSecondsProperty);
            this.ClearValue(AutoPvEStopTimeSecondsProperty);

            this.ClearValue(AllowTribeWarPvEProperty);
            this.ClearValue(AllowTribeWarCancelPvEProperty);
            this.ClearValue(AllowTribeAlliancesProperty);

            this.ClearValue(AllowCustomRecipesProperty);
            this.ClearValue(CustomRecipeEffectivenessMultiplierProperty);
            this.ClearValue(CustomRecipeSkillMultiplierProperty);

            this.ClearValue(EnableDiseasesProperty);
            this.ClearValue(NonPermanentDiseasesProperty);

            this.ClearValue(CraftXPMultiplierProperty);
            this.ClearValue(GenericXPMultiplierProperty);
            this.ClearValue(HarvestXPMultiplierProperty);
            this.ClearValue(KillXPMultiplierProperty);
            this.ClearValue(SpecialXPMultiplierProperty);
        }

        public void ResetSOTFSection()
        {
            this.ClearValue(SOTF_EnabledProperty);
            this.ClearValue(SOTF_OutputGameReportProperty);
            this.ClearValue(SOTF_GamePlayLoggingProperty);
            this.ClearValue(SOTF_DisableDeathSPectatorProperty);
            this.ClearValue(SOTF_OnlyAdminRejoinAsSpectatorProperty);
            this.ClearValue(SOTF_MaxNumberOfPlayersInTribeProperty);
            this.ClearValue(SOTF_BattleNumOfTribesToStartGameProperty);
            this.ClearValue(SOTF_TimeToCollapseRODProperty);
            this.ClearValue(SOTF_BattleAutoStartGameIntervalProperty);
            this.ClearValue(SOTF_BattleAutoRestartGameIntervalProperty);
            this.ClearValue(SOTF_BattleSuddenDeathIntervalProperty);

            this.ClearValue(SOTF_NoEventsProperty);
            this.ClearValue(SOTF_NoBossesProperty);
            this.ClearValue(SOTF_BothBossesProperty);
            this.ClearValue(SOTF_EvoEventIntervalProperty);
            this.ClearValue(SOTF_RingStartTimeProperty);
        }

        public void ResetStructuresSection()
        {
            this.ClearValue(StructureResistanceMultiplierProperty);
            this.ClearValue(StructureDamageMultiplierProperty);
            this.ClearValue(StructureDamageRepairCooldownProperty);
            this.ClearValue(PvPStructureDecayProperty);
            this.ClearValue(PvPZoneStructureDamageMultiplierProperty);
            this.ClearValue(MaxStructuresVisibleProperty);
            this.ClearValue(PerPlatformMaxStructuresMultiplierProperty);
            this.ClearValue(MaxPlatformSaddleStructureLimitProperty);
            this.ClearValue(OverrideStructurePlatformPreventionProperty);
            this.ClearValue(FlyerPlatformAllowUnalignedDinoBasingProperty);
            this.ClearValue(EnableStructureDecayPvEProperty);
            this.ClearValue(PvEStructureDecayDestructionPeriodProperty);
            this.ClearValue(PvEStructureDecayPeriodMultiplierProperty);
            this.ClearValue(AutoDestroyOldStructuresMultiplierProperty);
            this.ClearValue(ForceAllStructureLockingProperty);
            this.ClearValue(PassiveDefensesDamageRiderlessDinosProperty);
        }

        public void UpdateOverrideMaxExperiencePointsDino()
        {
            LevelList list = GetLevelList(LevelProgression.Dino);
            if (list == null || list.Count == 0)
                return;

            OverrideMaxExperiencePointsDino = list[list.Count - 1].XPRequired;
        }

        public void UpdateOverrideMaxExperiencePointsPlayer()
        {
            LevelList list = GetLevelList(LevelProgression.Player);
            if (list == null || list.Count == 0)
                return;

            OverrideMaxExperiencePointsPlayer = list[list.Count - 1].XPRequired;
        }
        #endregion

        #endregion
    }
}
