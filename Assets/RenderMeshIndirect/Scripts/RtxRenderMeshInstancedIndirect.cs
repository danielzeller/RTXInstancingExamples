using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

/*
 * We need to handle the RayTracingAccelerationStructure ourselves in order to add the instanced objects.
 * The scene view also needs Volume with RayTracingSettings AccelerationStructure = Manual.
 */

[ExecuteAlways]
public class RtxRenderMeshInstancedIndirect : MonoBehaviour {
    public Material material;
    public Mesh mesh;

    private RenderParams renderingParams;
    private GraphicsBuffer indirectArgumentsBuffer;
    private GraphicsBuffer indirectInstancedSpheres;

    RayTracingAccelerationStructure rtas;
    private RayTracingMeshInstanceConfig config;
    private RayTracingInstanceCullingConfig cullingConfig;
    private static readonly int SphereDataId = Shader.PropertyToID("_SphereData");
    private static readonly int RtxMatricesId = Shader.PropertyToID("_RtxMatrices");
    private static readonly int CountId = Shader.PropertyToID("_Count");

    private GraphicsBuffer rtxIndirectArgsBuffer;
    private GraphicsBuffer rtxIndirectInstancedMatrices;
    public ComputeShader updatePosition;

    private void OnEnable() { 
        if (rtas != null) return;

        //Rendering params for the instanced spheres
        renderingParams = new RenderParams(material) {
            worldBounds = new Bounds(Vector3.zero, 10000 * Vector3.one),
            reflectionProbeUsage = ReflectionProbeUsage.BlendProbes,
            shadowCastingMode = ShadowCastingMode.On,
            receiveShadows = true,
            lightProbeUsage = LightProbeUsage.BlendProbes,
            layer = gameObject.layer
        };
        var matrices = GenerateInstanceMatrices();

        //Create the indirect arguments buffer. In this example RenderMeshIndirect is typically used with an append buffer, but in this example we 
        // just use a regular ComputeBuffer which has a static length But the example is mainly for the RayTracingAccelerationStructure
        // part so it's good enough for now. 
        indirectArgumentsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, 1, GraphicsBuffer.IndirectDrawIndexedArgs.size);
        var commandData = new GraphicsBuffer.IndirectDrawIndexedArgs[1];
        commandData[0].indexCountPerInstance = mesh.GetIndexCount(0);
        commandData[0].instanceCount = (uint)matrices.Count;
        indirectArgumentsBuffer.SetData(commandData);


        //Add out instancing buffer with positions and a color attribute.
        indirectInstancedSpheres = new GraphicsBuffer(GraphicsBuffer.Target.Structured, matrices.Count, Marshal.SizeOf<SphereData>());
        SphereData[] sphereData = new SphereData[matrices.Count];
        for (int i = 0; i < matrices.Count; i++) {
            sphereData[i] = new SphereData {
                objectToWorld = matrices[i],
                worldToObject = matrices[i],
                color = getNiceColor(matrices[i].GetPosition())
            };
        }

        indirectInstancedSpheres.SetData(sphereData);


        //RayTracingAccelerationStructure settings
        rtxIndirectInstancedMatrices = new GraphicsBuffer(GraphicsBuffer.Target.Structured, matrices.Count, Marshal.SizeOf<Matrix4x4>());
        rtxIndirectInstancedMatrices.SetData(matrices);
        config = new RayTracingMeshInstanceConfig {
            mesh = mesh,
            material = material,
            subMeshFlags = RayTracingSubMeshFlags.Enabled | RayTracingSubMeshFlags.ClosestHitOnly,
            dynamicGeometry = false
        };
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

        rtxIndirectArgsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Raw, 1, 8);
        uint[] ints = new uint[2];
        ints[0] = 0; //Index 0 is startInstanceIndex
        ints[1] = (uint)matrices.Count; //Index 1 is instancesCount, this would typically bet set by GraphicsBuffer.CopyCount when using an appendBuffer.

        rtxIndirectArgsBuffer.SetData(ints);
    }

    //https://www.shadertoy.com/new quick'N dirty conversion
    private Vector4 getNiceColor(Vector3 position) {
        Vector3 uv = new Vector3(position.x / 10, position.y / 10, position.z / 10);

        return new Vector4(
            0.5f + 0.5f * Mathf.Cos(uv.x),
            0.5f + 0.5f * Mathf.Cos(uv.y + 2),
            0.5f + 0.5f * Mathf.Cos(uv.x + 4),
            1f);
    }

    public bool sleep;

    static readonly int GROUP_SIZE = 256;

    static int toCsGroups(int totalThreads) {
        return (totalThreads + (GROUP_SIZE - 1)) / GROUP_SIZE;
    }

    void Update() {
        //Update the poistions in a compute shader, this step can be skipped, it's just added to test movement
        updatePosition.SetBuffer(0, RtxMatricesId, rtxIndirectInstancedMatrices);
        updatePosition.SetBuffer(0, SphereDataId, indirectInstancedSpheres);
        updatePosition.SetInt(CountId, rtxIndirectInstancedMatrices.count);
        updatePosition.Dispatch(0, toCsGroups(rtxIndirectInstancedMatrices.count), 1, 1);

        //Draw the spheres using RenderMeshIndirect the matrix positions are read in the custom Shader
        material.SetBuffer(SphereDataId, indirectInstancedSpheres);
        Graphics.RenderMeshIndirect(renderingParams, mesh, indirectArgumentsBuffer);
        if (sleep) return;

        var hdCamera = HDCamera.GetOrCreate(GetComponent<Camera>());
        // Clear the contents of RayTracingAccelerationStructure from the previous frame
        rtas.ClearInstances();

        //Regular culling to draw the non instanced objects.  
        rtas.CullInstances(ref cullingConfig);
        //Add the instanced spheres 
        rtas.AddInstancesIndirect(config, rtxIndirectInstancedMatrices, rtxIndirectInstancedMatrices.count, rtxIndirectArgsBuffer);

        // Build the RayTracingAccelerationStructure
        rtas.Build(transform.position);

        // Assign it to the camera
        hdCamera.rayTracingAccelerationStructure = rtas;
    }

    void OnDisable() {
        rtas?.Dispose();
    }

    /*
     *Copied from https://github.com/INedelcu/RayTracingMeshInstancingHDRP/blob/main/Assets/Scripts/ManualRTASManager.cs
     */
    private List<Matrix4x4> GenerateInstanceMatrices() {
        Matrix4x4 m = Matrix4x4.identity;
        Vector3 pos = Vector3.zero;

        var matrices = new List<Matrix4x4>(32 * 32 * 32);

        for (int x = 0; x < 32; x++) //radial
        {
            for (int y = 0; y < 32; y++) //vertical
            {
                for (int z = 0; z < 32; z++) //circular
                {
                    var angle = y * Mathf.Pow(x * 0.004f, 2) + 2 * Mathf.PI * z / 31;
                    var radius = 5.0f + x * (1 + Mathf.Pow(y * 0.02f, 1.6f));
                    pos.x = radius * Mathf.Cos(angle);
                    pos.y = y;
                    pos.z = radius * Mathf.Sin(angle);
                    m.SetTRS(pos, Quaternion.identity, Vector3.one);
                    matrices.Add(m);
                    
                }
            }
        }

        return matrices;
    }
}

struct SphereData {
    public Matrix4x4 objectToWorld;
    public Matrix4x4 worldToObject;
    public Vector4 color;
}