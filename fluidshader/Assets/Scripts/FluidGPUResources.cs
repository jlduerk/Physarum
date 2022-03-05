using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FluidGPUResources
{

    //compute buffers
    public ComputeBuffer dye_buffer;    //where dye displays on screen
    public ComputeBuffer velocity_buffer;     //velocity per cell 
    public ComputeBuffer divergence_buffer;     // divergence in cell (calculated every frame)
    public ComputeBuffer pressure_buffer;     // pressure calculated from velocity and divergence
    public static ComputeBuffer buffer_ping;      //one buffer for solver loop
    public static ComputeBuffer buffer_pong;     //second solver loop buffer to switch back and forth to
    private int simulation_dimensions;     //resolution dimensions of screen/sim

 
    public FluidGPUResources()    // Default Constructor
    { 
        simulation_dimensions = 256;

    }

    public FluidGPUResources(FluidSimulation fso)    // real constructor
    {
        simulation_dimensions = (int)fso.simulation_dimension;
    }

    public void Release()  //destructor
    {
        velocity_buffer.Release();
        dye_buffer.Release();
        divergence_buffer.Release();
        pressure_buffer.Release();
        buffer_ping.Release();
        buffer_pong.Release();
    }
    

    //buffer initializer

    public void Create()
    {
        velocity_buffer = new ComputeBuffer(simulation_dimensions * simulation_dimensions, sizeof(float) * 4);
        dye_buffer = new ComputeBuffer(simulation_dimensions * simulation_dimensions, sizeof(float) * 4);
        divergence_buffer = new ComputeBuffer(simulation_dimensions * simulation_dimensions, sizeof(float) * 4);
        pressure_buffer = new ComputeBuffer(simulation_dimensions * simulation_dimensions, sizeof(float) * 4);
        buffer_pong = new ComputeBuffer(simulation_dimensions * simulation_dimensions, sizeof(float) * 4);
        buffer_ping = new ComputeBuffer(simulation_dimensions * simulation_dimensions, sizeof(float) * 4);

    }
}
