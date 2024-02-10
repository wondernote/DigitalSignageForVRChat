using UnityEngine;

public class MaterialApplier : MonoBehaviour
{
    [HideInInspector]
    public int selectedPresetIndex = 0;

    public Material blackMaterial;
    public Material whiteMaterial;
    public Material woodMaterial;

    public Material[] clockTimeMaterials;

    private string excludeTag = "ExcludeMaterial";

    public void ApplyMaterial(Material selectedMaterial)
    {
        Renderer[] childRenderers = GetComponentsInChildren<Renderer>();
        foreach (Renderer renderer in childRenderers)
        {
            if (renderer.gameObject.tag != excludeTag)
            {
                renderer.sharedMaterial = selectedMaterial;
            }
        }
    }
}
