using UnityEditor;
using UnityEngine;

public class FpsHorrorKitToURPConverter : EditorWindow
{
    [MenuItem("Tools/Convert FpsHorrorKit Materials to URP")]
    static void ShowWindow()
    {
        GetWindow<FpsHorrorKitToURPConverter>("HDRP→URP Converter");
    }

    void OnGUI()
    {
        GUILayout.Label("FpsHorrorKit HDRP → URP 마테리얼 변환", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Assets/FpsHorrorKit 폴더 내 모든 마테리얼의 텍스처를 URP 프로퍼티로 복사합니다.",
            MessageType.Info);

        if (GUILayout.Button("텍스처 재적용", GUILayout.Height(40)))
            FixTextures();
    }

    static void FixTextures()
    {
        Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
        if (urpLit == null)
        {
            Debug.LogError("[Converter] URP/Lit 셰이더를 찾을 수 없습니다.");
            return;
        }

        string[] guids = AssetDatabase.FindAssets("t:Material", new[] { "Assets/FpsHorrorKit" });
        int fixed_ = 0;

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) continue;

            // 아직 URP로 교체 안 된 것은 셰이더 교체
            if (mat.shader != urpLit)
                mat.shader = urpLit;

            // SerializedObject로 저장된 YAML 프로퍼티를 직접 읽고 복사
            SerializedObject so = new SerializedObject(mat);
            SerializedProperty texEnvs = so.FindProperty("m_SavedProperties.m_TexEnvs");
            if (texEnvs == null || !texEnvs.isArray) continue;

            // _BaseColorMap → _BaseMap
            CopyTexEntry(texEnvs, "_BaseColorMap", "_BaseMap");
            CopyTexEntry(texEnvs, "_MainTex",      "_BaseMap");   // fallback

            // _NormalMap → _BumpMap
            CopyTexEntry(texEnvs, "_NormalMap", "_BumpMap");

            // _EmissiveColorMap → _EmissionMap
            CopyTexEntry(texEnvs, "_EmissiveColorMap", "_EmissionMap");

            so.ApplyModifiedPropertiesWithoutUndo();

            // 노멀맵이 있으면 키워드 켜기
            if (mat.GetTexture("_BumpMap") != null)
                mat.EnableKeyword("_NORMALMAP");

            EditorUtility.SetDirty(mat);
            fixed_++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[Converter] {fixed_}개 마테리얼 텍스처 재적용 완료.");
        EditorUtility.DisplayDialog("완료", $"{fixed_}개 마테리얼 텍스처 재적용 완료.", "확인");
    }

    // texEnvs 배열에서 src 이름의 텍스처 레퍼런스를 dst 이름 항목에 복사
    static void CopyTexEntry(SerializedProperty texEnvs, string src, string dst)
    {
        SerializedProperty srcProp = null;
        SerializedProperty dstProp = null;

        for (int i = 0; i < texEnvs.arraySize; i++)
        {
            SerializedProperty entry = texEnvs.GetArrayElementAtIndex(i);
            string name = entry.displayName; // 키 이름
            // m_TexEnvs의 각 원소는 map 형태 — 첫 번째 자식이 키, 두 번째가 값
            SerializedProperty keyProp = entry.FindPropertyRelative("first");
            if (keyProp == null) continue;

            if (keyProp.stringValue == src) srcProp = entry.FindPropertyRelative("second");
            if (keyProp.stringValue == dst) dstProp = entry.FindPropertyRelative("second");
        }

        if (srcProp == null || dstProp == null) return;

        SerializedProperty srcTex = srcProp.FindPropertyRelative("m_Texture");
        SerializedProperty dstTex = dstProp.FindPropertyRelative("m_Texture");
        if (srcTex == null || dstTex == null) return;

        // 이미 텍스처가 있거나 소스가 비어있으면 스킵
        if (dstTex.objectReferenceValue != null) return;
        if (srcTex.objectReferenceValue == null) return;

        dstTex.objectReferenceValue = srcTex.objectReferenceValue;
    }
}
