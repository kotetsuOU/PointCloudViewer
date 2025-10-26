using UnityEngine;
using System.Collections.Generic;


public class MaterialController : MonoBehaviour
{
    [Tooltip("����ΏۂƂ���MeshRenderer�R���|�[�l���g�̃��X�g")]
    public List<MeshRenderer> targetRenderers;

    [Tooltip("�؂�ւ��Ɏg�p����}�e���A���̃��X�g")]
    public List<Material> materials;

    private int _currentMaterialIndex = 0;

    void Start()
    {
        if (targetRenderers == null || targetRenderers.Count == 0)
        {
            UnityEngine.Debug.LogWarning("����Ώۂ�MeshRenderer��1���ݒ肳��Ă��܂���B", this);
            return;
        }

        ApplyCurrentMaterial();
    }

    public void ChangeMaterial(int index)
    {
        if (materials == null || materials.Count == 0)
        {
            UnityEngine.Debug.LogWarning("�}�e���A�����X�g���ݒ肳��Ă��܂���B", this);
            return;
        }

        if (index < 0 || index >= materials.Count)
        {
            UnityEngine.Debug.LogError($"�}�e���A���̃C���f�b�N�X���͈͊O�ł�: {index}", this);
            return;
        }

        if (materials[index] == null)
        {
            UnityEngine.Debug.LogWarning($"�C���f�b�N�X {index} �̃}�e���A����NULL�ł��B", this);
            return;
        }

        _currentMaterialIndex = index;
        ApplyCurrentMaterial();
    }

    private void ApplyCurrentMaterial()
    {
        if (materials == null || materials.Count == 0 || targetRenderers == null || targetRenderers.Count == 0)
        {
            return;
        }

        if (_currentMaterialIndex >= materials.Count || materials[_currentMaterialIndex] == null)
        {
            return;
        }

        Material materialToApply = materials[_currentMaterialIndex];

        foreach (var renderer in targetRenderers)
        {
            if (renderer != null)
            {
                renderer.material = materialToApply;
            }
        }
    }

    public int GetCurrentMaterialIndex()
    {
        return _currentMaterialIndex;
    }
}