void InjectSetup_float(float3 ObjectSpacePosition, out float3 Out)
{
    Out = ObjectSpacePosition;
}

struct SpheraData
{
    float4x4 objectToWorld;
    float4x4 worldToObject;
    float4 color;
};

uniform StructuredBuffer<SpheraData> _SphereData;

void GetColor_float(float3 A, out float3 Out, out float4 Color)
{
    Out = A;
    #ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
		Color = _SphereData[unity_InstanceID].color;
    // #elif INSTANCING_ON 
    //     Color = _SphereData[InstanceIndex() - unity_BaseInstanceID].color;
    // #else
    #else
        // Color = float4(1, 0, 0, 0);
    Color = _SphereData[InstanceIndex() - unity_BaseInstanceID].color;
    #endif
}


void SetupInstancing()
{
    #ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED

    #ifdef unity_ObjectToWorld
    #undef unity_ObjectToWorld
    #endif

    #ifdef unity_WorldToObject
    #undef unity_WorldToObject
    #endif
    unity_ObjectToWorld = _SphereData[unity_InstanceID].objectToWorld;
    unity_WorldToObject = _SphereData[unity_InstanceID].worldToObject;
    #endif
}
