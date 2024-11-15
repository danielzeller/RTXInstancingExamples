using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

/*
 * We need to handle the RayTracingAccelerationStructure ourselves in order to add the instanced objects.
 * The scene view also needs Volume with RayTracingSettings AccelerationStructure = Manual.
 */

[ExecuteAlways]
public class RtxRenderMeshInstanced : MonoBehaviour {
    public Material material;
    public Mesh mesh;
    const int numSpheres = 10;

    public Matrix4x4[] instanceData = new Matrix4x4[numSpheres];
    private RenderParams instancingRenderParams;

    RayTracingAccelerationStructure rtas;
    private RayTracingMeshInstanceConfig config;
    private RayTracingInstanceCullingConfig cullingConfig;

    private void OnEnable() {
        if (rtas != null) return;

        //Instancing settings
        instancingRenderParams = new RenderParams(material);
        config = new RayTracingMeshInstanceConfig {
            mesh = mesh,
            material = material,
            subMeshFlags = RayTracingSubMeshFlags.Enabled | RayTracingSubMeshFlags.ClosestHitOnly,
            dynamicGeometry = false
        };

        //RayTracingAccelerationStructure settings
        //More settings here: https://docs.unity3d.com/2023.1/Documentation/ScriptReference/Rendering.RayTracingAccelerationStructure.CullInstances.html
        rtas = new RayTracingAccelerationStructure();
        cullingConfig = new RayTracingInstanceCullingConfig();
        cullingConfig.subMeshFlagsConfig.opaqueMaterials = RayTracingSubMeshFlags.Enabled | RayTracingSubMeshFlags.ClosestHitOnly;
        cullingConfig.subMeshFlagsConfig.alphaTestedMaterials = RayTracingSubMeshFlags.Enabled;
        cullingConfig.subMeshFlagsConfig.transparentMaterials = RayTracingSubMeshFlags.Disabled;

        RayTracingInstanceCullingTest cullingTest = new RayTracingInstanceCullingTest();
        cullingTest.allowAlphaTestedMaterials = true;
        cullingTest.allowOpaqueMaterials = true;
        cullingTest.allowTransparentMaterials = false;
        cullingTest.instanceMask = 255;
        cullingTest.layerMask = -1;
        cullingTest.shadowCastingModeMask = (1 << (int)ShadowCastingMode.Off) | (1 << (int)ShadowCastingMode.On) | (1 << (int)ShadowCastingMode.TwoSided);

        cullingConfig.instanceTests = new RayTracingInstanceCullingTest[1];
        cullingConfig.instanceTests[0] = cullingTest;
    }

    void Update() {
        //Draw a couple of instanced spheres
        for (int i = 0; i < numSpheres; ++i) {
            instanceData[i] = Matrix4x4.Translate(new Vector3(-4.5f + i, 1.0f, Mathf.Sin(Time.time + i)));
        }
        Graphics.RenderMeshInstanced(instancingRenderParams, mesh, 0, instanceData);

        var hdCamera = HDCamera.GetOrCreate(GetComponent<Camera>());
        // Clear the contents of RayTracingAccelerationStructure from the previous frame
        rtas.ClearInstances();

        //Regular culling to draw the non instanced objects.  
        rtas.CullInstances(ref cullingConfig);

        //Add the instanced spheres 
        rtas.AddInstances(config, instanceData);

        // Build the RayTracingAccelerationStructure
        rtas.Build(transform.position);

        // Assign it to the camera
        hdCamera.rayTracingAccelerationStructure = rtas;
    }

    void OnDisable() {
        rtas?.Dispose();
    }
}