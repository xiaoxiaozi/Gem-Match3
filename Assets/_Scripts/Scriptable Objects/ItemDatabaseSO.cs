using System;
using System.Collections.Generic;
using _Scripts.Data_Classes;
using Scripts;
using Scripts.BoosterActions;
using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// 物品数据库脚本化对象，用于存储和管理游戏中所有物品的数据
/// 包括普通物品、增强道具、合并动作、游戏板和背景等
/// </summary>
[CreateAssetMenu(fileName = "ItemDatabase", menuName = "Create ItemDatabase")]
public class ItemDatabaseSO : SerializedScriptableObject
{
    //Some Effects are defined explicitly because It seemed unnecessary to change whole ItemData architecture for these since every item has one particle effect most of the time. 
    [DictionaryDrawerSettings(KeyLabel = "Item ID", ValueLabel = "Item Data")]
    /// <summary>增强道具创建特效</summary>
    public readonly GameObject boosterCreationEffect;
    /// <summary>导弹命中特效</summary>
    public readonly GameObject missileHitEffect;
    /// <summary>导弹爆炸特效</summary>
    public readonly GameObject missileExplosionEffect;
    /// <summary>光球与光球合并爆炸特效</summary>
    public readonly GameObject LightBallLightBallExplosionEffect;
    /// <summary>TNT与火箭合并粒子特效</summary>
    public readonly GameObject TntRocketMergeParticleEffect;
    /// <summary>TNT与TNT爆炸特效</summary>
    public readonly GameObject TntTntExplosionEffect;
    /// <summary>星星粒子特效</summary>
    public readonly GameObject starParticleEffect;
    /// <summary>主事件UI特效</summary>
    public readonly GameObject mainEventUIEffect;
    /// <summary>金币ID</summary>
    public readonly int coinID;
    /// <summary>普通物品字典，键为物品ID，值为物品数据</summary>
    public readonly  Dictionary<int, ItemData> NormalItems = new Dictionary<int, ItemData>();
    /// <summary>增强道具字典，键为物品ID，值为物品数据</summary>
    public readonly Dictionary<int, ItemData> Boosters = new Dictionary<int, ItemData>();
    /// <summary>增强道具合并动作列表，每个元素为一个三元组(物品1ID,物品2ID,合并动作)</summary>
    public readonly List<Tuple<int,int,IItemMergeAction>> BoosterMergeAction = new List<Tuple<int, int,IItemMergeAction>>();
    /// <summary>游戏板字典，键为板ID，值为板精灵保存数据</summary>
    public readonly Dictionary<int, BoardSpriteSaveData> Boards = new Dictionary<int, BoardSpriteSaveData>();
    /// <summary>背景字典，键为背景ID，值为背景精灵</summary>
    public readonly Dictionary<int,Sprite> Backgrounds = new Dictionary<int, Sprite>();
    
    
    /// <summary>
    /// 初始化方法，将物品ID设置到预制体组件中
    /// </summary>
    [Button("Set Item ID's to Prefabs")]
    public void Initialize()
    {
        foreach (KeyValuePair<int,ItemData> item in NormalItems)
        {
            if(item.Value.ItemPrefab!=null)
                item.Value.ItemPrefab.GetComponent<IItem>().ItemID = item.Key;
            if (item.Value.ItemParticleEffect != null)
                item.Value.ItemParticleEffect.GetComponent<ItemParticleEffect>().ItemID = item.Key;
     
        }
        foreach (KeyValuePair<int,ItemData> item in Boosters)
        {
            if(item.Value.ItemPrefab!=null&&item.Value.ItemPrefab.GetComponent<IItem>()!=null)
                item.Value.ItemPrefab.GetComponent<IItem>().ItemID = item.Key;
            if (item.Value.ItemParticleEffect != null)
                item.Value.ItemParticleEffect.GetComponent<BoosterParticleEffect>().ItemID = item.Key;
        }
        
        Debug.Log("Item ID's are set.");
    }
    
    /// <summary>
    /// 获取指定ID的普通物品预制体
    /// </summary>
    /// <param name="id">物品ID</param>
    /// <returns>物品预制体GameObject</returns>
    public GameObject GetNormalItem(int id)
    {
        return NormalItems[id].ItemPrefab;
    }
    
    /// <summary>
    /// 获取指定ID的普通物品粒子特效
    /// </summary>
    /// <param name="id">物品ID</param>
    /// <returns>粒子特效GameObject</returns>
    public GameObject GetNormalItemParticleEffect(int id)
    {
        return NormalItems[id].ItemParticleEffect;
    }
    
    /// <summary>
    /// 获取指定ID的物品动作
    /// </summary>
    /// <param name="id">物品ID</param>
    /// <returns>物品动作接口实现，如果ID不存在则返回null</returns>
    public IItemAction GetNormalItemAction(int id)
    {
        if (NormalItems.ContainsKey(id))
        {
            return NormalItems[id].ItemAction;
            
        }
        if(Boosters.ContainsKey(id))
        {
            return Boosters[id].ItemAction;
        }
        return null;
     
    }
    
    /// <summary>
    /// 获取指定ID的增强道具动作
    /// </summary>
    /// <param name="id">增强道具ID</param>
    /// <returns>增强道具动作接口实现</returns>
    public IItemAction GetBoosterItemAction(int id)
    {
        return Boosters[id].ItemAction;
    }
    
    /// <summary>
    /// 获取指定ID的增强道具预制体
    /// </summary>
    /// <param name="id">增强道具ID</param>
    /// <returns>增强道具预制体GameObject</returns>
    public GameObject GetBooster(int id)
    {
        return Boosters[id].ItemPrefab;
    }
    
    /// <summary>
    /// 获取指定ID的增强道具粒子特效
    /// </summary>
    /// <param name="id">增强道具ID</param>
    /// <returns>增强道具粒子特效GameObject</returns>
    public GameObject GetBoosterParticleEffect(int id)
    {
        return Boosters[id].ItemParticleEffect; 
    }
    
    /// <summary>
    /// 获取指定ID的普通物品精灵
    /// </summary>
    /// <param name="id">物品ID</param>
    /// <returns>物品精灵Sprite</returns>
    public Sprite GetItemSprite(int id)
    {
        return NormalItems[id].ItemSprite;
    }
    
    /// <summary>
    /// 获取指定ID的增强道具精灵
    /// </summary>
    /// <param name="id">增强道具ID</param>
    /// <returns>增强道具精灵Sprite</returns>
    public Sprite GetBoosterSprite(int id)
    {
        return Boosters[id].ItemSprite;
    }
    
    /// <summary>
    /// 获取增强道具创建特效
    /// </summary>
    /// <returns>增强道具创建特效GameObject</returns>
    public GameObject GetBoosterCreationEffect()
    {
        return boosterCreationEffect;
    }

    /// <summary>
    /// 获取指定ID的游戏板精灵保存数据
    /// </summary>
    /// <param name="id">游戏板ID</param>
    /// <returns>游戏板精灵保存数据</returns>
    public BoardSpriteSaveData GetBoardSpriteData(int id)
    {
        return Boards[id];
    }
    
}
