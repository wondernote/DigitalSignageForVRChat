using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(MaterialApplier))]
public class MaterialApplierEditor : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        MaterialApplier applier = (MaterialApplier)target;

        string[] options = new string[] { "ブラック", "ホワイト", "ウッド" };

        EditorGUI.BeginChangeCheck();
        int selectedPresetIndex = EditorGUILayout.Popup("カラー選択", applier.selectedPresetIndex, options);

        if (EditorGUI.EndChangeCheck())
        {
            applier.selectedPresetIndex = selectedPresetIndex;

            Material selectedMaterial = null;
            Color shaderLightColor = new Color(1f, 1f, 1f, 1f);
            Color shaderBgColor = new Color(0.03f, 0.03f, 0.03f, 1f);

            switch (selectedPresetIndex)
            {
                case 0:
                    selectedMaterial = applier.blackMaterial;
                    shaderLightColor = new Color(1f, 1f, 1f, 1f);
                    shaderBgColor = new Color(0.21f, 0.22f, 0.24f, 1f);
                    break;
                case 1:
                    selectedMaterial = applier.whiteMaterial;
                    shaderLightColor = new Color(1f, 1f, 1f, 1f);
                    shaderBgColor = new Color(0.6f, 0.62f, 0.65f, 1f);
                    break;
                case 2:
                    selectedMaterial = applier.woodMaterial;
                    shaderLightColor = new Color(1f, 0.95f, 0.9f, 1f);
                    shaderBgColor = new Color(0.4f, 0.32f, 0.24f, 1f);
                    break;
            }

            if (selectedMaterial != null)
            {
                applier.ApplyMaterial(selectedMaterial);
                EditorUtility.SetDirty(selectedMaterial);
            }

            if (applier.clockTimeMaterials != null && applier.clockTimeMaterials.Length > 0)
            {
                foreach (Material mat in applier.clockTimeMaterials)
                {
                    if (mat != null)
                    {
                        mat.SetColor("_CustomLightColor", shaderLightColor);
                        mat.SetColor("_CustomBgColor", shaderBgColor);
                        EditorUtility.SetDirty(mat);
                    }
                }
            }

            EditorUtility.SetDirty(applier);
        }
    }
}
