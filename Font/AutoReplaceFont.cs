using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SG;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>
/// 字体替换工具
/// </summary>
public class AutoReplaceFontTool : EditorWindow
{
    private enum FontType
    {
        None,
        Text,
        TMP
    }
    #region Attribute

    /// <summary>
    /// 待替换预制体列表
    /// </summary>
    private List<GameObject> m_prefabList = new List<GameObject>();

    /// <summary>
    /// 预制体路径信息字典
    /// </summary>
    private Dictionary<string, string> m_prefabPathDic = new Dictionary<string, string>();
    
    /// <summary>
    /// 预制体状态列表
    /// </summary>
    private List<bool> m_prefabStateList = new List<bool>();

    /// <summary>
    /// 字体资源对象
    /// </summary>
    private static Object s_oldFontObj;
    private static Object s_newFontObj;

    private Vector2 m_scrollViewPos;

    private bool m_needShowScrollView;

    #endregion

    #region Engine Methods

    [MenuItem("Tools/字体替换工具")]
    static void AutoReplaceFont()
    {
        AutoReplaceFontTool window = (AutoReplaceFontTool)GetWindow(typeof(AutoReplaceFontTool));
        
        window.titleContent.text = "字体替换工具";
        
        window.Show();
    }

    private void OnEnable()
    {
        s_oldFontObj = null;
        s_newFontObj = null;
        m_needShowScrollView = false;
        
        DirectoryInfo di = new DirectoryInfo(Application.dataPath);
        FileInfo[] fileInfoArray = di.GetFiles("*.prefab", SearchOption.AllDirectories);

        foreach (var fileInfo in fileInfoArray)
        {
            string path = fileInfo.FullName.Replace("\\", "/").Replace(Application.dataPath, "Assets");

            if (!m_prefabPathDic.Keys.Contains(fileInfo.Name))
            {
                m_prefabPathDic.Add(fileInfo.Name, path);
            }
            else
            {
                m_prefabPathDic[fileInfo.Name] = path;
            }   
        }
    }
    
    private void OnGUI()
    {
        RefreshUI();
    }

    #endregion

    #region Privates Methods

    private void RefreshUI()
    {
        #region TargetFontArea

        GUILayout.Space(20f);
        
        GUILayout.BeginHorizontal();
        {
            GUILayout.Label("旧字体：", GUILayout.Width(80f));
            
            s_oldFontObj = EditorGUILayout.ObjectField(s_oldFontObj, typeof(Object), true);
            
            GUILayout.Space(10f);
            
            if (GUILayout.Button("搜索", GUILayout.MaxWidth(80f)))
            {
                if (s_oldFontObj == null)
                {
                    if (EditorUtility.DisplayDialog("错误提示", "资源对象为空，无法查找对象", "确定"))
                    {
                        return;
                    } 
                }
                
                FontType fontType = GetFontType(s_oldFontObj);
                if (fontType == FontType.None)
                {
                    if (EditorUtility.DisplayDialog("错误提示", "资源类型需要为字体，无法查找对象", "确定"))
                    {
                        return;
                    }
                }

                UpdateTargetPrefabList(ref m_prefabList);

                UpdatePrefabStateList(ref m_prefabStateList, true);

                m_needShowScrollView = true;
            }
        }
        GUILayout.EndHorizontal();
        
        GUILayout.Space(20f);
        
        GUILayout.BeginHorizontal();
        {
            GUILayout.Label("新字体：", GUILayout.Width(80f));
            
            s_newFontObj = EditorGUILayout.ObjectField(s_newFontObj, typeof(Object), true);
            
            GUILayout.Space(90f);
        }
        GUILayout.EndHorizontal();

        #endregion

        #region ScrollViewArea

        if (m_needShowScrollView)
        {
            GUILayout.Space(20f);

            GUILayout.BeginHorizontal();
            {
                int selectNum = 0;
                
                for (int i = 0; i < m_prefabStateList.Count; i++)
                {
                    if (m_prefabStateList[i]) selectNum++;
                }
                
                GUILayout.Label($"选择需要替换字体的预制体, 已选中{selectNum}个");
                
                if (GUILayout.Button("全选", GUILayout.MaxWidth(80f)))
                {
                    if (m_prefabList is { Count: > 0 })
                    {
                        for (int i = 0; i < m_prefabList.Count; i++)
                        {
                            m_prefabStateList[i] = true;
                        }
                    }
                }
            }
            GUILayout.EndHorizontal();
        
            GUILayout.Space(10f);

            m_scrollViewPos = EditorGUILayout.BeginScrollView(m_scrollViewPos, GUILayout.Height(400f));
            {
                if (m_needShowScrollView)
                {
                    for (int i = 0; i < m_prefabList.Count; i++)
                    {
                        string prefabName = m_prefabList[i].gameObject.name;
                        m_prefabStateList[i] = EditorGUILayout.ToggleLeft(prefabName, m_prefabStateList[i]);
                    }
                }
            }
        
            EditorGUILayout.EndScrollView();   
        }

        #endregion

        #region ReplaceButtonArea

        GUILayout.Space(20f);

        if (GUILayout.Button("替换", GUILayout.MinHeight(30f)))
        {
            if (s_oldFontObj == null || s_newFontObj == null)
            {
                if (EditorUtility.DisplayDialog("错误提示", "资源对象为空，无法替换", "确定"))
                {
                    return;
                } 
            }
                
            FontType oldFontType = GetFontType(s_oldFontObj);
            FontType newFontType = GetFontType(s_newFontObj);
            
            if (oldFontType == FontType.None || newFontType == FontType.None || oldFontType != newFontType)
            {
                if (EditorUtility.DisplayDialog("错误提示", "资源类型错误，无法替换", "确定"))
                {
                    return;
                }
            }

            if (EditorUtility.DisplayDialog("提示", "是否开始替换字体", "确定", "取消"))
            {
                if (m_prefabList.Count == 0)
                {
                    UpdateTargetPrefabList(ref m_prefabList);
                
                    UpdatePrefabStateList(ref m_prefabStateList, true);
                }
                
                int successNum = ReplacePrefabFont(oldFontType);
                if (successNum > 0)
                {
                    if (EditorUtility.DisplayDialog("提示", $"字体替换成功，总共替换{successNum}个预制体", "确定"))
                    {
                        CleanData();
                    }
                }
                else
                {
                    EditorUtility.DisplayDialog("错误提示", "没有找到符合条件的预制体", "确定");
                }
            }
        }

        #endregion
        
    }

    private FontType GetFontType(Object _obj)
    {
        if (_obj.GetType() == typeof(Font)) return FontType.Text;
        
        if (_obj.GetType() == typeof(TMP_FontAsset)) return FontType.TMP;

        return FontType.None;
    }

    private int ReplacePrefabFont(FontType _fontType)
    {
        int successNum = 0;
        
        for (int i = 0; i < m_prefabList.Count; i++)
        {
            if(m_prefabStateList[i] == false) continue;

            GameObject go = m_prefabList[i];
            
            switch (_fontType)
            {
                case FontType.Text:
                    Text[] texts = go.GetComponentsInChildren<Text>();
                    
                    foreach (var text in texts)
                    {
                        if (text.font != null)
                        {
                            text.font = (Font)s_newFontObj;
                            EditorUtility.SetDirty(text);
                        }
                    }

                    try
                    {
                        PrefabUtility.SavePrefabAsset(go);
                        successNum++;
                    }
                    catch (Exception e)
                    {
                        Log.Error($"预制体：{go.name} 替换Text字体失败" + "\n" + e.Message);
                    }
                    break;
                case FontType.TMP:
                    var textMeshPros = go.GetComponentsInChildren<TextMeshProUGUI>(true);
                    
                    foreach (var textMeshProUGUI in textMeshPros)
                    {
                        if (textMeshProUGUI != null)
                        {
                            textMeshProUGUI.font = (TMP_FontAsset)s_newFontObj;
                            EditorUtility.SetDirty(textMeshProUGUI);
                        }
                    }
                    
                    try
                    {
                        PrefabUtility.SavePrefabAsset(go);
                        successNum++;
                    }
                    catch (Exception e)
                    {
                        Log.Error($"预制体：{go.name} 替换TMP字体失败" + "\n" + e.Message);
                    }
                    break;
            }
        }

        return successNum;
    }

    private void UpdateTargetPrefabList(ref List<GameObject> _prefabInfoList)
    {
        _prefabInfoList.Clear();

        FontType fontType = GetFontType(s_oldFontObj);
                
        foreach (var path in m_prefabPathDic.Values)
        {
            GameObject go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    
            if (go != null)
            {
                switch (fontType)
                {
                    case FontType.Text:
                        Text[] texts = go.GetComponentsInChildren<Text>(true);
                        if (texts is { Length: > 0 })
                        {
                            foreach (var text in texts)
                            {
                                if (text.font != null && text.font.name.Contains(s_oldFontObj.name))
                                {
                                    _prefabInfoList.Add(go);
                                    break;
                                }
                            }
                        }
                        break;
                    case FontType.TMP:
                        var tmpComponents = go.GetComponentsInChildren<TextMeshProUGUI>(true);
                        if (tmpComponents is { Length: > 0 })
                        {
                            foreach (var tmp in tmpComponents)
                            {
                                if (tmp.font != null && tmp.font.name.Contains(s_oldFontObj.name))
                                {
                                    _prefabInfoList.Add(go);
                                    break;
                                }
                            }
                        }
                        break;
                }
            }
        }
    }

    private void UpdatePrefabStateList(ref List<bool> _prefabStateList, bool _state)
    {
        _prefabStateList.Clear();

        for (int i = 0; i < m_prefabList.Count; i++)
        {
            _prefabStateList.Add(_state);
        }
    }

    private void CleanData()
    {
        m_prefabList.Clear();
        m_prefabStateList.Clear();
        s_newFontObj = null;
        s_oldFontObj = null;
        m_needShowScrollView = false;
    }
    #endregion
}
