using System;
using UnityEngine;

namespace Cqw.MultilayerFramework.UI
{
    /// <summary>游戏设置服务接口（阶段八 GameSettings 接入后替换实现）。</summary>
    public interface IGameSettingsService
    {
        int GetInt(string key, int defaultValue);
        void SetInt(string key, int value);
        float GetFloat(string key, float defaultValue);
        void SetFloat(string key, float value);
        bool GetBool(string key, bool defaultValue);
        void SetBool(string key, bool value);
        string GetString(string key, string defaultValue);
        void SetString(string key, string value);
        void Save();
        void RevertAll();
    }

    /// <summary>PlayerPrefs 占位实现（阶段八替换为 GameSettings SO/JSON 持久化）。</summary>
    public class PlayerPrefsGameSettingsService : IGameSettingsService
    {
        public int GetInt(string key, int defaultValue) => PlayerPrefs.GetInt(key, defaultValue);
        public void SetInt(string key, int value) => PlayerPrefs.SetInt(key, value);
        public float GetFloat(string key, float defaultValue) => PlayerPrefs.GetFloat(key, defaultValue);
        public void SetFloat(string key, float value) => PlayerPrefs.SetFloat(key, value);
        public bool GetBool(string key, bool defaultValue) => PlayerPrefs.GetInt(key, defaultValue ? 1 : 0) == 1;
        public void SetBool(string key, bool value) => PlayerPrefs.SetInt(key, value ? 1 : 0);
        public string GetString(string key, string defaultValue) => PlayerPrefs.GetString(key, defaultValue);
        public void SetString(string key, string value) => PlayerPrefs.SetString(key, value);
        public void Save() => PlayerPrefs.Save();
        public void RevertAll()
        {
            Debug.Log("[GameSettings] RevertAll()（占位：恢复默认值需阶段八定义默认表）。");
        }
    }

    /// <summary>设置服务定位器。</summary>
    public static class GameSettingsServiceLocator
    {
        static IGameSettingsService service;
        public static IGameSettingsService Service => service ??= new PlayerPrefsGameSettingsService();
        public static void Set(IGameSettingsService impl) => service = impl;
    }
}
