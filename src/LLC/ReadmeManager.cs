﻿using System;
using System.IO;
using System.Linq;
using HarmonyLib;
using Il2CppSystem.Collections.Generic;
using LocalSave;
using MainUI;
using MainUI.NoticeUI;
using Server;
using SimpleJSON;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UObject = UnityEngine.Object;

namespace LimbusLocalize.LLC;

public static class ReadmeManager
{
    public static NoticeUIPopup NoticeUIInstance;
    public static RedDotWriggler RedDotNotice;
    public static List<Notice> ReadmeList = new();
    public static Dictionary<string, Sprite> ReadmeSprites;
    public static System.Collections.Generic.Dictionary<string, Action> ReadmeActions;

    static ReadmeManager()
    {
        InitReadmeList();
        InitReadmeSprites();
        ReadmeActions = new System.Collections.Generic.Dictionary<string, Action>
        {
            {
                "Action_Issues", () =>
                {
                    LLCMod.CopyLog();
                    LLCMod.OpenGamePath();
                    Application.OpenURL(LLCMod.LLCLink + "/issues?q=is:issue");
                }
            }
        };
    }

    public static void UIInitialize()
    {
        var close = Close;
        NoticeUIInstance._popupPanel.closeEvent.AddListener(close);
        NoticeUIInstance._arrowScroll.Initialize();
        NoticeUIInstance._titleViewManager.Initialized();
        NoticeUIInstance._contentViewManager.Initialized();
        NoticeUIInstance.btn_back._onClick.AddListener(close);
        var eventNoticeOnClick = NoticeUIInstance.EventTapClickEvent;
        var systemNoticeOnClick = NoticeUIInstance.SystemTapClickEvent;
        NoticeUIInstance.btn_eventNotice._onClick.AddListener(eventNoticeOnClick);
        NoticeUIInstance.btn_systemNotice._onClick.AddListener(systemNoticeOnClick);
        NoticeUIInstance.btn_systemNotice.GetComponentInChildren<UITextDataLoader>(true).enabled = false;
        NoticeUIInstance.btn_systemNotice.GetComponentInChildren<TextMeshProUGUI>(true).text = "更新公告";
        NoticeUIInstance.btn_eventNotice.GetComponentInChildren<UITextDataLoader>(true).enabled = false;
        NoticeUIInstance.btn_eventNotice.GetComponentInChildren<TextMeshProUGUI>(true).text = "公告";
    }

    public static void Open()
    {
        NoticeUIInstance.Open();
        NoticeUIInstance._popupPanel.Open();
        var notices = ReadmeList;
        NoticeUIInstance._systemNotices = notices.FindAll((Func<Notice, bool>)Findsys);
        NoticeUIInstance._eventNotices = notices.FindAll((Func<Notice, bool>)Findeve);
        NoticeUIInstance.EventTapClickEvent();
        NoticeUIInstance.btn_eventNotice.Cast<UISelectedButton>().SetSelected(true);
        return;

        bool Findsys(Notice x)
        {
            return x.noticeType == NOTICE_TYPE.System;
        }

        bool Findeve(Notice x)
        {
            return x.noticeType == NOTICE_TYPE.Event;
        }
    }

    public static void InitReadmeSprites()
    {
        ReadmeSprites = new Dictionary<string, Sprite>();

        foreach (var fileInfo in new DirectoryInfo(LLCMod.ModPath + "/Localize/Readme").GetFiles()
                     .Where(f => f.Extension is ".jpg" or ".png"))
        {
            Texture2D texture2D = new(2, 2);
            ImageConversion.LoadImage(texture2D, File.ReadAllBytes(fileInfo.FullName));
            var sprite = Sprite.Create(texture2D, new Rect(0f, 0f, texture2D.width, texture2D.height),
                new Vector2(0.5f, 0.5f));
            var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(fileInfo.FullName);
            texture2D.name = fileNameWithoutExtension;
            sprite.name = fileNameWithoutExtension;
            UObject.DontDestroyOnLoad(sprite);
            sprite.hideFlags |= HideFlags.HideAndDontSave;
            ReadmeSprites[fileNameWithoutExtension] = sprite;
        }
    }

    public static void InitReadmeList()
    {
        ReadmeList.Clear();
        foreach (var notices in JSONNode.Parse(File.ReadAllText(LLCMod.ModPath + "/Localize/Readme/Readme.json"))[0]
                     .AsArray.m_List)
            ReadmeList.Add(new Notice(JsonUtility.FromJson<NoticeFormat>(notices.ToString()), LOCALIZE_LANGUAGE.KR));
    }

    public static void Close()
    {
        UserLocalSaveDataRoot.Instance.NoticeRedDotSaveModel.Save();
        NoticeUIInstance._popupPanel.Close();
        UpdateNoticeRedDot();
    }

    public static void UpdateNoticeRedDot()
    {
        RedDotNotice?.gameObject.SetActive(IsValidRedDot());
    }

    public static bool IsValidRedDot()
    {
        var i = 0;
        var count = ReadmeList.Count;
        while (i < count)
        {
            if (!UserLocalSaveDataRoot.Instance.NoticeRedDotSaveModel.TryCheckId(ReadmeList[i].ID)) return true;
            i++;
        }

        return false;
    }

    #region 公告相关

    [HarmonyPatch(typeof(UserLocalNoticeRedDotModel), nameof(UserLocalNoticeRedDotModel.InitNoticeList))]
    [HarmonyPrefix]
    private static bool InitNoticeList(UserLocalNoticeRedDotModel __instance, List<int> severNoticeList)
    {
        UpdateChecker.CheckReadmeUpdate();
        for (var i = 0; i < __instance.GetDataList().Count; i++)
        {
            __instance.idList.RemoveAll((Func<int, bool>)Func);
            continue;

            bool Func(int x)
            {
                return !severNoticeList.Contains(x) && ReadmeList.FindAll((Func<Notice, bool>)Func2).Count == 0;

                bool Func2(Notice x2)
                {
                    return x2.ID == x;
                }
            }
        }

        __instance.Save();
        UpdateNoticeRedDot();
        return false;
    }

    [HarmonyPatch(typeof(NoticeUIPopup), nameof(NoticeUIPopup.Initialize))]
    [HarmonyPostfix]
    private static void NoticeUIPopupInitialize(NoticeUIPopup __instance)
    {
        if (NoticeUIInstance) return;
        var noticeUIPopupInstance = UObject.Instantiate(__instance, __instance.transform.parent);
        NoticeUIInstance = noticeUIPopupInstance;
        UIInitialize();
    }

    [HarmonyPatch(typeof(MainLobbyUIPanel), nameof(MainLobbyUIPanel.Initialize))]
    [HarmonyPostfix]
    private static void MainLobbyUIPanelInitialize(MainLobbyUIPanel __instance)
    {
        var uiButtonInstance = UObject.Instantiate(__instance.button_notice, __instance.button_notice.transform.parent)
            .Cast<MainLobbyRightUpperUIButton>();
        RedDotNotice = uiButtonInstance.gameObject.GetComponentInChildren<RedDotWriggler>(true);
        UpdateNoticeRedDot();
        uiButtonInstance._onClick.RemoveAllListeners();
        var onClick = Open;
        uiButtonInstance._onClick.AddListener(onClick);
        uiButtonInstance.transform.SetSiblingIndex(1);
        var spriteSetting = ScriptableObject.CreateInstance<ButtonSprites>();
        spriteSetting._enabled = ReadmeSprites["Readme_Zero_Button"];
        spriteSetting._hover = ReadmeSprites["Readme_Zero_Button"];
        uiButtonInstance.spriteSetting = spriteSetting;
        var transform = __instance.button_notice.transform.parent;
        var layoutGroup = transform.GetComponent<HorizontalLayoutGroup>();
        layoutGroup.childScaleHeight = true;
        layoutGroup.childScaleWidth = true;
        for (var i = 0; i < transform.childCount; i++) transform.GetChild(i).localScale = new Vector3(0.77f, 0.77f, 1f);
    }

    [HarmonyPatch(typeof(NoticeUIContentImage), nameof(NoticeUIContentImage.SetData))]
    [HarmonyPrefix]
    private static bool ImageSetData(NoticeUIContentImage __instance, string formatValue)
    {
        if (!formatValue.StartsWith("Readme_")) return true;
        var image = ReadmeSprites[formatValue];
        __instance.gameObject.SetActive(true);
        __instance.SetImage(image);
        return false;
    }

    [HarmonyPatch(typeof(NoticeUIContentHyperLink), nameof(NoticeUIContentHyperLink.OnPointerClick))]
    [HarmonyPrefix]
    private static bool HyperLinkOnPointerClick(NoticeUIContentHyperLink __instance)
    {
        var url = __instance.tmp_main.text;
        if (url.StartsWith("<link"))
        {
            var startIndex = url.IndexOf('=');
            if (startIndex != -1)
            {
                var endIndex = url.IndexOf('>', startIndex + 1);
                if (endIndex != -1) url = url.Substring(startIndex + 1, endIndex - startIndex - 1);
            }

            if (url.StartsWith("Action_"))
            {
                ReadmeActions[url]?.Invoke();
                return false;
            }
        }

        Application.OpenURL(url);
        return false;
    }

    #endregion
}