using UnityEngine;

public class StageBuilder : MonoBehaviour
{
    [Header("Materials")]
    [SerializeField] private Material groundMaterial;
    [SerializeField] private Material testAreaMaterial;
    [SerializeField] private Material obstacleMaterial;

    [Header("Build")]
    [SerializeField] private bool rebuildOnStart = true;

    private Transform stageRoot;

    private void Start()
    {
        if (!rebuildOnStart)
        {
            return;
        }

        BuildStage();
    }

    private void BuildStage()
    {
        EnsureStageRoot();
        ClearStage();

        CreateBlock("GroundPad", new Vector3(0f, -0.5f, 70f), new Vector3(120f, 1f, 180f), groundMaterial);
        CreateBlock("StartPad", new Vector3(0f, 0f, 8f), new Vector3(18f, 0.2f, 28f), testAreaMaterial);

        CreateBlock("SweeperLaneA", new Vector3(0f, 0f, 42f), new Vector3(18f, 0.2f, 36f), testAreaMaterial);
        CreateBlock("SweeperLaneB", new Vector3(12f, 0f, 66f), new Vector3(22f, 0.2f, 28f), testAreaMaterial);
        CreateBlock("SweeperLaneC", new Vector3(26f, 0f, 86f), new Vector3(20f, 0.2f, 24f), testAreaMaterial);

        CreateSlalomRock("Slalom01", new Vector3(-6f, 0.9f, 38f), new Vector3(2.2f, 1.8f, 2.2f), -8f);
        CreateSlalomRock("Slalom02", new Vector3(6f, 0.9f, 50f), new Vector3(2.2f, 1.8f, 2.2f), 14f);
        CreateSlalomRock("Slalom03", new Vector3(-6f, 0.9f, 62f), new Vector3(2.2f, 1.8f, 2.2f), -12f);
        CreateSlalomRock("Slalom04", new Vector3(6f, 0.9f, 74f), new Vector3(2.2f, 1.8f, 2.2f), 10f);
        CreateSlalomRock("Slalom05", new Vector3(-6f, 0.9f, 86f), new Vector3(2.2f, 1.8f, 2.2f), -10f);

        CreateBlock("RampRunUp", new Vector3(-24f, 0f, 36f), new Vector3(14f, 0.2f, 34f), testAreaMaterial);
        CreateRamp("GentleRamp", new Vector3(-24f, 0.55f, 56f), new Vector3(14f, 1.2f, 14f), 8f, testAreaMaterial);
        CreateBlock("RampLanding", new Vector3(-24f, 1.15f, 72f), new Vector3(14f, 0.2f, 24f), testAreaMaterial);

        CreateBump("Bump01", new Vector3(22f, 0.18f, 26f), new Vector3(4f, 0.35f, 4f));
        CreateBump("Bump02", new Vector3(28f, 0.12f, 28f), new Vector3(4f, 0.25f, 4f));
        CreateBump("Bump03", new Vector3(34f, 0.2f, 30f), new Vector3(4f, 0.4f, 4f));
        CreateBump("Bump04", new Vector3(24f, 0.16f, 36f), new Vector3(4f, 0.3f, 4f));
        CreateBump("Bump05", new Vector3(30f, 0.1f, 38f), new Vector3(4f, 0.2f, 4f));
        CreateBump("Bump06", new Vector3(36f, 0.18f, 40f), new Vector3(4f, 0.35f, 4f));
        CreateBump("Bump07", new Vector3(22f, 0.14f, 46f), new Vector3(4f, 0.28f, 4f));
        CreateBump("Bump08", new Vector3(28f, 0.2f, 48f), new Vector3(4f, 0.38f, 4f));
        CreateBump("Bump09", new Vector3(34f, 0.12f, 50f), new Vector3(4f, 0.24f, 4f));
    }

    private void EnsureStageRoot()
    {
        if (stageRoot != null)
        {
            return;
        }

        Transform existing = transform.Find("GeneratedStage");
        if (existing != null)
        {
            stageRoot = existing;
            return;
        }

        GameObject root = new GameObject("GeneratedStage");
        root.transform.SetParent(transform, false);
        stageRoot = root.transform;
    }

    private void ClearStage()
    {
        for (int i = stageRoot.childCount - 1; i >= 0; i--)
        {
            Destroy(stageRoot.GetChild(i).gameObject);
        }
    }

    private void CreateBlock(string objectName, Vector3 position, Vector3 scale, Material material)
    {
        GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
        block.name = objectName;
        block.transform.SetParent(stageRoot, false);
        block.transform.position = position;
        block.transform.localScale = scale;
        ApplyMaterial(block, material);
    }

    private void CreateRamp(string objectName, Vector3 position, Vector3 scale, float xAngle, Material material)
    {
        GameObject ramp = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ramp.name = objectName;
        ramp.transform.SetParent(stageRoot, false);
        ramp.transform.position = position;
        ramp.transform.rotation = Quaternion.Euler(xAngle, 0f, 0f);
        ramp.transform.localScale = scale;
        ApplyMaterial(ramp, material);
    }

    private void CreateSlalomRock(string objectName, Vector3 position, Vector3 scale, float yRotation)
    {
        GameObject rock = GameObject.CreatePrimitive(PrimitiveType.Cube);
        rock.name = objectName;
        rock.transform.SetParent(stageRoot, false);
        rock.transform.position = position;
        rock.transform.rotation = Quaternion.Euler(0f, yRotation, 0f);
        rock.transform.localScale = scale;
        ApplyMaterial(rock, obstacleMaterial);
    }

    private void CreateBump(string objectName, Vector3 position, Vector3 scale)
    {
        GameObject bump = GameObject.CreatePrimitive(PrimitiveType.Cube);
        bump.name = objectName;
        bump.transform.SetParent(stageRoot, false);
        bump.transform.position = position;
        bump.transform.localScale = scale;
        ApplyMaterial(bump, testAreaMaterial);
    }

    private void ApplyMaterial(GameObject target, Material material)
    {
        if (material == null)
        {
            return;
        }

        Renderer renderer = target.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = material;
        }
    }
}
