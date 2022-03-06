using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FluidSimManager : MonoBehaviour
{

    public FluidSimulation simulator;
    private FluidGPUResources resources;

    void Start()
    {
        //initializing the simulation
        simulator.StokeNavierShader = (ComputeShader)Resources.Load("NavierStokes");
        simulator.SolverShader = (ComputeShader)Resources.Load("JacobiSolver");
        simulator.StructuredBufferToTextureShader = (ComputeShader)Resources.Load("StructuredBufferToTexture");
        simulator.UserInputShader = (ComputeShader)Resources.Load("UserInput");
        simulator.StructuredBufferUtilityShader = (ComputeShader)Resources.Load("StructuredBufferUtility");
        simulator.Initialize();
        
        //creating all the buffers :,)
        resources = new FluidGPUResources(simulator);
        resources.Create();

        simulator.AddUserForce(resources.velocity_buffer);
        simulator.Diffuse(resources.velocity_buffer);
        simulator.Project(resources.velocity_buffer, resources.divergence_buffer, resources.pressure_buffer);
        simulator.Advect(resources.velocity_buffer, resources.velocity_buffer, 1.0f);
        simulator.Project(resources.velocity_buffer, resources.divergence_buffer, resources.pressure_buffer);


        simulator.AddDye(resources.dye_buffer);
        simulator.Advect(resources.dye_buffer, resources.velocity_buffer, 0.992f);
        simulator.Diffuse(resources.dye_buffer);

        simulator.Visualize(resources.dye_buffer);

        simulator.BindCommandBuffer();
    }
    
    void OnDisable()
    {
        simulator.Release();
        resources.Release();
    }

    void Update()
    {
        simulator.Tick(Time.deltaTime);
    }
}
