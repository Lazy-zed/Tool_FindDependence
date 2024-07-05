using System.Text;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.IO;
using System.Reflection;

public enum CheckType//选中文件的类型
{
    IsPrefab,
    IsResource,
}
public enum FindType//要查找的类型
{
    FindPrefab,
    FindBanResource,
    FindNoBanResource,
    FindAllResource,
}
public enum BanType//不被查找的资源类型
{
    cs,
    dll,
    ttf,
}
public class FindPrefabOrResourcePath : Editor
{
    //预制查找引用资源
    [MenuItem("Assets/Find Dependency In Asset/Find Resources/BanResources",false)]
    public static void FindBanResources()
    {
        FindReferenceType(FindType.FindBanResource);
    }
    [MenuItem("Assets/Find Dependency In Asset/Find Resources/NoBanResources",false)]
    public static void FindNoBanResources()
    {
        FindReferenceType(FindType.FindNoBanResource);
    }
    [MenuItem("Assets/Find Dependency In Asset/Find Resources/AllResources",false)]
    public static void FindAllResources()
    {
        FindReferenceType(FindType.FindAllResource);
    }
    [MenuItem("Assets/Find Dependency In Asset/Find Resources/AllResources",true)]
    [MenuItem("Assets/Find Dependency In Asset/Find Resources/NoBanResources",true)]
    [MenuItem("Assets/Find Dependency In Asset/Find Resources/BanResources",true)]
    public static bool IsPrefab()
    {
        return CheckSelectedType(CheckType.IsPrefab);
    }

    //资源查找引用预制
    [MenuItem("Assets/Find Dependency In Asset/Find Prefabs",false)]
    public static void FindPrefabs()
    {
        FindReferenceType(FindType.FindPrefab);
    }
    [MenuItem("Assets/Find Dependency In Asset/Find Prefabs",true)]
    public static bool IsResource()
    {
        return CheckSelectedType(CheckType.IsResource);
    }

    //双击控制台查找路径
    [UnityEditor.Callbacks.OnOpenAssetAttribute(1)]
    private static bool OnOpenAsset(int instanceID, int line)
    {
        //Debug.Log("instanceID：" + instanceID + "   line:" + line);
        if(EditorUtility.InstanceIDToObject(instanceID).name == "FindPrefabOrResourcePath" && line != -1)//解决资源打开和高亮的冲突
        {
            FindConsoleSelected();
            return true;
        }  
        return false;
    }

    //判断选中资源是否可以进行查找
    private static bool CheckSelectedType(CheckType type)
    {
#if UNITY_EDITOR
        Object selectedObject = Selection.activeObject;
        string assetPath = AssetDatabase.GetAssetPath(selectedObject);
        if(assetPath != null)
        {
            if(!Directory.Exists(Application.dataPath + assetPath.Substring(6)))
            {
                bool isPrefab = PrefabUtility.IsPartOfAnyPrefab(selectedObject);
                switch(type)
                {
                    case CheckType.IsPrefab :
                        return isPrefab;
                    case CheckType.IsResource :
                        return !isPrefab;
                }
            }
        }
        return false;
#endif
    }

    //查找对应引用资源
    private static void FindReferenceType(FindType type)
    {
#if UNITY_EDITOR
        Object selectedObject = Selection.activeObject;
        string assetPath = AssetDatabase.GetAssetPath(selectedObject);
        switch(type)
        {
            case FindType.FindPrefab :
                string[] rootPath = GetJsonInfo().pathList;
                string[] allPrefabsGUID = AssetDatabase.FindAssets(" t:Prefab", rootPath);
                StringBuilder prefabInfo = new StringBuilder();
                foreach (string prefabGUID in allPrefabsGUID)
                {
                    string prefabPath = AssetDatabase.GUIDToAssetPath(prefabGUID);
                    string[] dependencies = AssetDatabase.GetDependencies(prefabPath);
                    foreach (string path in dependencies )
                    {
                        if(path == assetPath)
                        {
                            Debug.Log(prefabPath);
                            prefabInfo.Append(prefabPath + "\n");
                            break;
                        }
                    }
                }
                FindRecord(FindType.FindPrefab, prefabInfo, selectedObject.name);
                break;
            case FindType.FindBanResource :
                FindResourceFromType(FindType.FindBanResource, assetPath, selectedObject.name);
                break;
            case FindType.FindNoBanResource :
                FindResourceFromType(FindType.FindNoBanResource, assetPath, selectedObject.name);
                break;
            case FindType.FindAllResource :
                FindResourceFromType(FindType.FindAllResource, assetPath, selectedObject.name);
                break;
        }
#endif
    }

    //根据类型查找资源
    private static void FindResourceFromType(FindType type, string assetPath, string objName)
    {
#if UNITY_EDITOR
        string[] resoueces = AssetDatabase.GetDependencies(assetPath);
        Dictionary<string, List<string>> resourceDic = ResourcesSort(resoueces);
        string banList = string.Join("，",(BanType[])System.Enum.GetValues(typeof(BanType)));
        StringBuilder resourcesInfo = new StringBuilder();
        resourcesInfo.Append(type == FindType.FindAllResource? "":"禁止资源名单为：" + banList + "\n");
        foreach(var pathList in resourceDic.Values)
        {
            if(pathList != null)
            {
                foreach(string path in pathList)
                {
                    string pathType = GetResourceType(path);
                    bool isBanType = System.Enum.TryParse(pathType, true, out BanType banType);
                    if(type == FindType.FindBanResource && !isBanType)
                        break;
                    else if(type == FindType.FindNoBanResource && isBanType) 
                        break;
                    Debug.Log(path);
                    resourcesInfo.Append(path + "\n");
                }
            }
        }
        FindRecord(type, resourcesInfo, objName);
#endif
    }

    //控制台双击日志中设为高亮
    private static void FindConsoleSelected()
    {
#if UNITY_EDITOR
        System.Type m_ConsoleWindowType = System.Type.GetType("UnityEditor.ConsoleWindow,UnityEditor");//获取到控制台窗口
        FieldInfo m_ActiveTextInfo = m_ConsoleWindowType.GetField("m_ActiveText", BindingFlags.Instance | BindingFlags.NonPublic);//m_ActiveText包含了当前Log的全部信息
        FieldInfo m_ConsoleWindowFileInfo = m_ConsoleWindowType.GetField("ms_ConsoleWindow", BindingFlags.Static | BindingFlags.NonPublic);//ms_ConsoleWindow 是ConsoleWindow的对象字段
        var windowInstance = m_ConsoleWindowFileInfo.GetValue(null);//从对象字段中得到这个对象
        var activeText = m_ActiveTextInfo.GetValue(windowInstance); //得到Log信息,用于后面解析
        if(activeText.ToString() != "")
        {
            string consoleLog = activeText.ToString();
            int targetRear = consoleLog.IndexOf("UnityEngine.Debug");
            if(targetRear > 0)
            {
                string targetPath = consoleLog.Substring(0, targetRear - 1);
                Object obj = AssetDatabase.LoadAssetAtPath<Object>(targetPath);
                if (obj != null)
                    EditorGUIUtility.PingObject(obj);//高亮 
            }
        }
#endif
    }

    //历史查找记录
    private static void FindRecord(FindType type, StringBuilder recordInfo, string objName)
    {
        const string folderName = "FindResourcesRecord";
        string rootPath = Path.GetDirectoryName(Application.dataPath);
        string folderPath = Path.Combine(rootPath, folderName);
        if(!Directory.Exists(folderPath))
        {
            Directory.CreateDirectory(folderPath);//创建文件夹
        }
        string des = type == FindType.FindPrefab?"查找资源" + objName + "的所有预制引用":"查找预制" + objName + "的所有引用资源";
        Debug.Log(des);
        string fileName = System.DateTime.Now.ToString("yyyy-MM-dd HH-mm-ss") + "查找" + objName + ".txt";
        string filePath = Path.Combine(folderPath, fileName);
        if(!File.Exists(filePath))
        {
            File.WriteAllText(filePath, recordInfo.ToString());//创建记录文件
            Debug.Log("记录文件名:" + fileName);    
            Debug.Log("文件夹路径:" + folderPath);  
            string[] files = Directory.GetFiles(folderPath, "*.txt");
            int fileLimit = GetJsonInfo().fileLimitMax;
            if(files.Length > fileLimit)
            {
                int clearCount = GetJsonInfo().fileClearCount;
                List<string> sortFilesInfoList = SortFileByCreationTime(files);
                for(int i = 0; i < clearCount && i < sortFilesInfoList.Count; i++)
                    File.Delete(sortFilesInfoList[i]);              
                Debug.Log("文件数量溢出，已自动清理最早创建的" + clearCount + "个txt文件");
            }
        }
    }

    //文件按创建时间排序
    private static List<string> SortFileByCreationTime(string[] files)
    {
        List<string> sortFileList = new List<string>(files);
        sortFileList.Sort(
            (a,b) =>
            {
                FileInfo infoA = new FileInfo(a);
                FileInfo infoB = new FileInfo(b);
                return infoA.CreationTime.CompareTo(infoB.CreationTime);
            }
        );
        return sortFileList;
    }
    //资源根据类型排序
    private static Dictionary<string, List<string>> ResourcesSort(string[] resoueces)
    {
        Dictionary<string, List<string>> resourceDic = new Dictionary<string, List<string>>();
        foreach (string path in resoueces )
        {
            string dicKey = GetResourceType(path);
            if(resourceDic.ContainsKey(dicKey))//排序
            {
                if(resourceDic[dicKey] == null)
                {
                    List<string> dicValue = new List<string>();
                }
                resourceDic[dicKey].Add(path);
            }
            else
            {
                List<string> dicValue = new List<string>();
                dicValue.Add(path);
                resourceDic.Add(dicKey,dicValue);
            }
        }
        return resourceDic;
    }

    //从资料路径截取资源类型
    private static string GetResourceType(string path)
    {
            int strStart = path.LastIndexOf(".") + 1;
            int strEnd = path.Length;
            return path.Substring(strStart, strEnd - strStart);
    }

    //解析json
    private static FolderSettingInfo GetJsonInfo()
    {
        const string jsonPath = "/Editor/FindDependency/FolderSetting.json";
        string jsonFilePath = Application.dataPath + jsonPath;
        string jsonInfo = File.ReadAllText(jsonFilePath, Encoding.UTF8);
        FolderSettingInfo jsonObj = JsonUtility.FromJson<FolderSettingInfo>(jsonInfo);
        return jsonObj;
    }
}



