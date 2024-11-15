2 Examples of instancing in Unity with RayTracing. 

Instancing doesn't work out of the box with raytricaing in Unity.

Example 1:
Simple example using Graphics.RenderMeshInstanced and RayTracingAccelerationStructure.AddInstances to add it to the RayTracing acceleration structure.

Example 2: 
Same thing only with Graphica.RenderMeshIndirect and RayTracingAccelerationStructure.AddInstancesIndirect. This example animates some spheres using a Compute Shader.

Both examples manually handle RayTracingAccelerationStructure in order to be able to add the instances. To do so you need to add a Volume with RayTracingSettings and AccelerationStructure = Manual.

HDRP only. Let me know if it helped you in any way. I found reading trough examples and documentation quite cumbersome so I hope this example helps someone! 

References: 

https://github.com/INedelcu/RayTracingMeshInstancingHDRP

https://www.shadertoy.com/new

Cheers 

https://github.com/user-attachments/assets/f9e9ccae-4867-4bb3-9666-726ef7ead9de

