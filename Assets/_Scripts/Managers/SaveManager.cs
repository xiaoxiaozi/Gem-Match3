using System;
using System.Collections.Generic;
using System.IO;
using _Scripts.Core.Interfaces;
using _Scripts.Data_Classes;
using Data;
using DefaultNamespace;
using Rimaethon.Scripts.Managers;
using Rimaethon.Scripts.Utility;
using Sirenix.Serialization;
using UnityEngine;

//Get and Set Methods in this class are just for testing purposes. In a real game, these methods should be server authoritative .
// 保存管理系统，负责游戏数据的持久化存储和加载
public class SaveManager : PersistentSingleton<SaveManager>,ITimeDependent
{
    [SerializeField] private ItemDatabaseSO itemDatabase; // 物品数据库引用
    [SerializeField] private List<int> _boosterIdsToInitialize; // 初始化时创建的booster ID列表
    [SerializeField] private List<int> _powerUpIdsToInitialize; // 初始化时创建的powerup ID列表
    [SerializeField] private int _initialBoosterCount = 10; // booster初始数量
    
    // 各种数据存储路径配置
    private readonly string eventDataFolder = "Assets/Data/Events/";
    private readonly string levelDataFolder = "Assets/Data/Levels/";
    private readonly string gameDataPath = "Assets/Data/General/GameData";
    private readonly string userDataPath = "Assets/Data/General/UserData";
    private readonly string eventDataName = "MainEvent";
    private readonly string Extension = ".json";
    
    // 核心数据对象
    private UserData _userData; // 玩家个人数据
    private GameData _gameData; // 游戏全局数据
    private EventData mainEventData; // 主活动数据
    private LevelData _currentLevelData; // 当前关卡数据
    
    // 状态标志
    private bool _hasMainEvent; // 是否有主活动
    private bool _hasNewLevel; // 是否有新关卡
    private bool _isDataInitialized; // 数据是否已初始化

    // 编辑器调试选项
#if UNITY_EDITOR
    public bool shouldStartFromSpecificLevel; // 是否从特定关卡开始
    public int currentLevelToStartIfExists; // 要开始的关卡编号
#endif

    private void OnEnable()
    {
        CheckAndCreateData();
        EventManager.Instance.AddHandler(GameEvents.OnGameSceneLoaded, CheckAndCreateData);
        EventManager.Instance.AddHandler(GameEvents.OnMenuSceneLoaded, CheckAndCreateData);
    }

    private void OnDisable()
    {
        if (EventManager.Instance == null) return;
        EventManager.Instance.RemoveHandler(GameEvents.OnGameSceneLoaded, CheckAndCreateData);
        EventManager.Instance.RemoveHandler(GameEvents.OnMenuSceneLoaded, CheckAndCreateData);
    }

    /// <summary>
    /// 核心数据初始化方法
    /// 1. 检查用户数据文件是否存在
    /// 2. 初始化/加载游戏数据
    /// 3. 加载主事件数据
    /// 4. 加载当前关卡数据
    /// </summary>
    private void CheckAndCreateData()
    {
        // 检查用户数据文件
        if (!File.Exists(userDataPath+Extension))
        {
            InitializeUserData(); // 初始化用户数据
            SaveToJson( _userData,userDataPath); // 保存到JSON
        }
        else
        {
            _userData=LoadFromJson<UserData>(userDataPath+Extension);
            // 更新booster无限状态时间
            foreach (var boosterData in _userData.BoosterAmounts.Values)
            {
                boosterData.unlimitedDuration -= DateTimeOffset.UtcNow.ToUnixTimeSeconds() - boosterData.unlimitedStartTime;
                boosterData.unlimitedStartTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                if(boosterData.unlimitedDuration<=0)
                {
                    boosterData.isUnlimited = false;
                    boosterData.unlimitedDuration = 0;
                    boosterData.unlimitedStartTime = 0;
                }
            }
        }
        
        // 检查游戏数据文件
        if (!File.Exists(gameDataPath))
        {
            InitializeGameData();
            SaveToJson(_gameData,gameDataPath);
        }
        else
        {
            _gameData=LoadFromJson<GameData>(gameDataPath);
        }
        
        // 检查活动数据文件
        if(File.Exists(eventDataFolder+eventDataName+Extension))
        {
            mainEventData=LoadFromJson<EventData>(eventDataFolder+eventDataName+Extension);
            _hasMainEvent = true;
        }else
        {
           Debug.LogError("Main Event Data is missing");
        }
#if UNITY_EDITOR
        if (shouldStartFromSpecificLevel)
        {
            if(_gameData.NumberOfLevels>=currentLevelToStartIfExists)
            {
                _userData.currentLevel = currentLevelToStartIfExists;
            }
            else
            {
                Debug.LogError("Current Level is greater than the number of levels");
            }
        }
#endif

        // 检查关卡数据文件
        if(File.Exists(levelDataFolder+_userData.currentLevel+Extension))
        {
            var data = File.ReadAllBytes(levelDataFolder + _userData.currentLevel + Extension);
            _currentLevelData= SerializationUtility.DeserializeValue<LevelData>(data, DataFormat.JSON);
            _hasNewLevel = true;
        }
        else
        {
            _hasNewLevel = false;
        }
        _isDataInitialized = true;


    }

    #region User Data Getters and Setters

    #region Resources
    public int GetCoinAmount()
    {
        return _userData.coinAmount;
    }
    public void AdjustCoinAmount(int amount)
    {
        _userData.coinAmount += amount;
        EventManager.Instance.Broadcast(GameEvents.OnCoinAmountChanged);
        SaveToJson<UserData>( _userData,userDataPath);
    }

    public int GetHeartAmount()
    {
        return _userData.heartAmount;
    }
    public void AdjustHeartAmount(int amount)
    {
        _userData.heartAmount += amount;
        EventManager.Instance.Broadcast(GameEvents.OnHeartAmountChanged);
        SaveToJson<UserData>( _userData,userDataPath);
    }
    public void AddTimeToUnlimitedHeartTime(int duration)
    {
        _userData.firstHeartNotBeingFullUnixTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        _userData.heartRefillTimeInSeconds += duration;
        SaveToJson<UserData>( _userData,userDataPath);
    }
    public bool HasUnlimitedHearts()
    {
        return _userData.hasUnlimitedHearts;
    }
    public string GetUnlimitedHeartTime()
    {
        return DateTimeOffset.FromUnixTimeSeconds(_userData.unlimitedHeartDuration).DateTime.TimeOfDay.ToString();
    }
    public int GetMaxHeartAmount()
    {
        return _userData.maxHeartAmount;
    }
    public int GetHeartRefillTimeInSeconds()
    {
        return _userData.heartRefillTimeInSeconds;
    }
    public long GetFirstHeartNotBeingFullUnixTime()
    {
        return _userData.firstHeartNotBeingFullUnixTime;
    }
    public int GetStarAmount()
    {
        return _userData.starAmount;
    }
    public void AdjustStarAmount(int amount)
    {
        _userData.starAmount += amount;
        EventManager.Instance.Broadcast(GameEvents.OnStarAmountChanged);
        SaveToJson<UserData>( _userData,userDataPath);
    }

    #endregion

    #region Boosters and PowerUps

    public int GetBoosterAmount(int boosterId)
    {
        if (_userData.BoosterAmounts.ContainsKey(boosterId))
        {
            return _userData.BoosterAmounts[boosterId].boosterAmount;
        }
        return 0;
    }
    public int GetPowerUpAmount(int powerUpId)
    {
        if (_userData.PowerUpAmounts.ContainsKey(powerUpId))
        {
            return _userData.PowerUpAmounts[powerUpId];
        }
        return 0;
    }
    //Increase and decrease with certain amount. Such as -1 when booster used and +3 when booster bought/earned
    public void AdjustBoosterAmount(int boosterId, int count)
    {
        if (_userData.BoosterAmounts.ContainsKey(boosterId))
        {
            _userData.BoosterAmounts[boosterId].boosterAmount += count;
        }
        EventManager.Instance.Broadcast(GameEvents.OnBoosterAmountChanged);

        SaveToJson<UserData>( _userData,userDataPath);
    }
    public void AdjustPowerUpAmount(int powerUpId, int count)
    {
        if (_userData.PowerUpAmounts.ContainsKey(powerUpId))
        {
            _userData.PowerUpAmounts[powerUpId] += count;
        }
        EventManager.Instance.Broadcast(GameEvents.OnPowerUpAmountChanged);
        SaveToJson<UserData>( _userData,userDataPath);
    }

    public void AddTimeToUnlimitedBooster(int boosterId, int duration)
    {
        if (_userData.BoosterAmounts.ContainsKey(boosterId))
        {
            if(!_userData.BoosterAmounts[boosterId].isUnlimited)
            {
                _userData.BoosterAmounts[boosterId].isUnlimited = true;
                _userData.BoosterAmounts[boosterId].unlimitedStartTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                _userData.BoosterAmounts[boosterId].unlimitedDuration = duration;
            }
            else
            {
                _userData.BoosterAmounts[boosterId].unlimitedDuration += duration;
            }
            _userData.BoosterAmounts[boosterId].unlimitedDuration += duration;
        }
        SaveToJson<UserData>( _userData,userDataPath);
    }
    public bool HasUnlimitedBooster(int boosterId)
    {
        if (_userData.BoosterAmounts.ContainsKey(boosterId))
        {
            return _userData.BoosterAmounts[boosterId].isUnlimited;
        }
        return false;
    }
    public string GetUnlimitedBoosterTime(int boosterId)
    {
        if (_userData.BoosterAmounts.ContainsKey(boosterId))
        {
           return DateTimeOffset.FromUnixTimeSeconds(_userData.BoosterAmounts[boosterId].unlimitedDuration).DateTime.TimeOfDay.ToString();
        }
        return "";
    }
    #endregion

    #region Settings
    public bool IsMusicOn()
    {
        return _userData.isMusicOn;
    }
    public bool IsSfxOn()
    {
        if(!_isDataInitialized)
            CheckAndCreateData();
        return _userData.isSfxOn;
    }
    public bool IsHintOn()
    {
        return _userData.isHintOn;
    }
    public bool IsNotificationOn()
    {
        return _userData.isNotificationOn;
    }
    public void SetMusic(bool value)
    {
        _userData.isMusicOn = value;
        SaveToJson<UserData>( _userData,userDataPath);
        EventManager.Instance.Broadcast(GameEvents.OnMusicToggle);
    }
    public void SetSFX(bool value)
    {
        _userData.isSfxOn = value;
        SaveToJson<UserData>( _userData,userDataPath);
    }
    public void SetHint(bool value)
    {
        _userData.isHintOn = value;
        SaveToJson<UserData>( _userData,userDataPath);
    }
    public void SetNotification(bool value)
    {
        _userData.isNotificationOn = value;
        SaveToJson<UserData>( _userData,userDataPath);
    }



    #endregion

    #endregion

    #region Level Data Getters and Setters
    public int GetCurrentLevelName()
    {
        return _userData.currentLevel;
    }
    public int[] GetCurrentLevelGoalIds()
    {
        return _currentLevelData.GoalSaveData.GoalIDs;
    }
    public int[] GetCurrentLevelGoalAmounts()
    {
        return _currentLevelData.GoalSaveData.GoalAmounts;
    }
    public bool HasNewLevel()
    {
        CheckAndCreateData();
        return _hasNewLevel;
    }
    public int GetLevelIndex()
    {
        return _userData.currentLevel;
    }
    public void IncreaseLevelIndex()
    {
        _userData.currentLevel++;
        SaveToJson(_userData, userDataPath);
    }

    public LevelData GetCurrentLevelData()
    {
        return _currentLevelData;
    }
    #endregion

    #region Main Event Getters and Setters

    public EventData GetMainEventData()
    {
        return mainEventData;
    }
    public bool HasMainEvent()
    {
        return _hasMainEvent;
    }
    public void SaveMainEventData(EventData eventData)
    {
        mainEventData = eventData;
        SaveToJson(mainEventData, eventDataFolder + eventDataName );
    }

    #endregion

    #region Game Data Getters and Setters
    public string GetVersion()
    {
        return _gameData.Version;
    }
    public int GetNumberOfLevels()
    {
        return _gameData.NumberOfLevels;
    }

    #endregion

    #region Initializers
    private void InitializeUserData()
    {
        _userData = new UserData();
        _userData.BoosterAmounts = new Dictionary<int, BoosterData>();
        foreach (var item in _boosterIdsToInitialize)
        {
            _userData.BoosterAmounts.Add(item, new BoosterData
            {
                boosterAmount = _initialBoosterCount,
                isUnlimited = false,
                unlimitedStartTime = 0,
                unlimitedDuration = 0
            });
        }
        _userData.PowerUpAmounts = new Dictionary<int, int>();
        foreach (var item in _powerUpIdsToInitialize)
        {
            _userData.PowerUpAmounts.Add(item, _initialBoosterCount);
        }
    }
    private void InitializeGameData()
    {
        _gameData= new GameData
        {
            Version = Application.version,
            NumberOfLevels = GetNumberOfFilesInFolder(levelDataFolder)
        };
    }

    #endregion

    #region Helpers
    private int GetNumberOfFilesInFolder(string folderPath)
    {
        return Directory.GetFiles(folderPath).Length;
    }
    public void SaveToJson<T>(T data, string path)
    {
        var serializedData = SerializationUtility.SerializeValue(data, DataFormat.JSON);
        File.WriteAllBytes(path + Extension, serializedData);
    }

    /// <summary>
    /// JSON反序列化加载方法
    /// </summary>
    /// <returns>加载的反序列化对象</returns>
    public T LoadFromJson<T>(string path)
    {
        var bytes = File.ReadAllBytes(path);
        return SerializationUtility.DeserializeValue<T>(bytes, DataFormat.JSON);
    }
    #endregion

    /// <summary>
    /// 时间更新回调接口实现
    /// 1. 更新加速器无限持续时间
    /// 2. 处理心形资源自动恢复
    /// 3. 检查主事件过期状态
    /// </summary>
    public void OnTimeUpdate(long currentTime)
    {
        foreach (var boosterData in _userData.BoosterAmounts.Values)
        {
            boosterData.unlimitedDuration--;
            if(boosterData.unlimitedDuration<=0)
            {
                boosterData.isUnlimited = false;
                boosterData.unlimitedDuration = 0;
                boosterData.unlimitedStartTime = 0;
            }
        }
        if (currentTime - _userData.firstHeartNotBeingFullUnixTime >= _userData.heartRefillTimeInSeconds)
        {
            _userData.firstHeartNotBeingFullUnixTime = currentTime;
            if (_userData.heartAmount < _userData.maxHeartAmount)
            {
                _userData.heartAmount++;
            }
            if (_userData.hasUnlimitedHearts)
            {
                _userData.unlimitedHeartDuration = currentTime - _userData.unlimitedHeartStartTime+_userData.unlimitedHeartDuration;
            }
        }

        if (_hasMainEvent)
        {
            if (mainEventData.eventStartUnixTime + mainEventData.eventDuration <= currentTime)
            {
                _hasMainEvent = false;
            }
            SaveMainEventData(mainEventData);
        }
        SaveToJson<UserData>(_userData,userDataPath);
    }
}
