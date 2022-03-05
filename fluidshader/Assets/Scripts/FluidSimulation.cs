﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public enum FieldType
{
    Velocity, Pressure, Dye
}


[System.Serializable]
public class FluidSimulation
{
    
    [HideInInspector] public ComputeShader StokeNavierShader;    //advection code, and divergence calcs through velocity and pressure
    [HideInInspector] public ComputeShader SolverShader;    //Jacobi solver (can be added to in future work)
    [HideInInspector] public ComputeShader StructuredBufferToTextureShader;    //utility kernels for buffer -> texture
    [HideInInspector] public ComputeShader UserInputShader;     //User input kernels
    [HideInInspector] public ComputeShader StructuredBufferUtilityShader;   //utility kernels for filters etc.

    [Header("Light Sensor Attributes")]
    public ReadSensor lightSensor;
    public bool usingLightSensor = false;

    [Header("Simulation Attributes")]
    public uint canvas_dimension = 512;          // Resolution of the render target used at the end, this can be lower or higher than the actual simulation grid resoltion
    public uint simulation_dimension = 512;          // Resolution of the simulation grid
    public uint solver_iteration_num = 120;           // Number of iterations the solvers go through, increase this for more accurate simulation, and decrease for better performance
    public float viscosity = 0.02f;         // This factor describes the fluids resistence towards motion, higher viscosity value will cause greater diffusion. You can seprate the viscosity of dye from velocity, atm both are the same
    public float force_strength = 1.0f;         // multiplyer on your mouse movement, higher number leads to strong force
    public float force_radius = 10;            // how large the area around your mouse is which recieves the force
    public float force_falloff = 1;            // This creates a soft brush of a sort for force application
    public float dye_radius = 20.0f;         // Exact same  behaviour as the force one
    public float dye_falloff = 5.0f;         // Exact same  behaviour as the force one

    [HideInInspector] public KeyCode dyeKey;
    [HideInInspector] public KeyCode forceKey;

    //___________
    // private

    private Camera main_cam;

    private CommandBuffer sim_command_buffer;
    private RenderTexture visualizationTexture;

    // The handles for different kernels
    private int handle_dye;
    private int handle_pressure_st2tx;
    private int handle_velocity_st2tx;
    private int handle_dye_st2tx;
    private int handle_Jacobi;
    private int handle_copyBuffer; //structured buffer
    private int handle_clearBuffer; //also structured
    private int handle_addForce; //with mouse
    private int handle_advection;
    private int handle_divergence;
    private int handle_calculateDivergence;

    // Info used for input through mouse 
    private Vector2 mousePreviousPos;
    private bool mousePreviousOutOfBound;
    // ------------------------------------------------------------------
    // CONSTRUCTOR

    public FluidSimulation()                       // Default Constructor
    {
        canvas_dimension = 512;
        simulation_dimension = 512;
        solver_iteration_num = 120;
        force_radius = 10;
        force_falloff = 1;
        dye_radius = 20.0f;
        dye_falloff = 5.0f;

        dyeKey = KeyCode.Mouse0;
        forceKey = KeyCode.Mouse1;

    }

    public FluidSimulation(FluidSimulation other)   // Copy Constructor
    {
        canvas_dimension = other.canvas_dimension;
        simulation_dimension = other.simulation_dimension;
        solver_iteration_num = other.solver_iteration_num;
        force_radius = other.force_radius;
        force_falloff = other.force_falloff;
        dye_radius = other.dye_radius;
        dye_falloff = other.dye_falloff;

        dyeKey = KeyCode.Mouse0;
        forceKey = KeyCode.Mouse1;
    }

    // ------------------------------------------------------------------
    // DESTRUCTOR


    public void Release()             // Make sure to call this function at the end of your implementation on end play
    {
        visualizationTexture.Release();
        ComputeShaderUtility.Release();
    }
    // ------------------------------------------------------------------
    // INITALISATION

    public void Initialize()          // This function needs to be called before you start using the fluid engine
    {

        ComputeShaderUtility.Initialize();

        main_cam = Camera.main;
        if (main_cam == null) Debug.LogError("Could not find main camera, make sure the camera is tagged as main");


        mousePreviousPos = GetCurrentMouseInSimulationSpace();

        visualizationTexture = new RenderTexture((int)canvas_dimension, (int)canvas_dimension, 0)
        {
            enableRandomWrite = true,
            useMipMap = false,
        };

        visualizationTexture.Create();
        // -----------------------
        // Setting kernel handles

        handle_dye = ComputeShaderUtility.GetKernelHandle(UserInputShader, "AddDye");
        handle_pressure_st2tx = ComputeShaderUtility.GetKernelHandle(StructuredBufferToTextureShader, "PressureStructeredToTextureBillinearR32");
        handle_velocity_st2tx = ComputeShaderUtility.GetKernelHandle(StructuredBufferToTextureShader, "VelocityStructeredToTextureBillinearRG32");
        handle_dye_st2tx = ComputeShaderUtility.GetKernelHandle(StructuredBufferToTextureShader, "DyeStructeredToTextureBillinearRGB8");
        handle_copyBuffer = ComputeShaderUtility.GetKernelHandle(StructuredBufferUtilityShader, "Copy_StructuredBuffer");
        handle_Jacobi = ComputeShaderUtility.GetKernelHandle(SolverShader, "Jacobi_Solve");
        handle_clearBuffer = ComputeShaderUtility.GetKernelHandle(StructuredBufferUtilityShader, "Clear_StructuredBuffer");
        handle_addForce = ComputeShaderUtility.GetKernelHandle(UserInputShader, "AddForce_mouse");
        handle_advection = ComputeShaderUtility.GetKernelHandle(StokeNavierShader, "advection");
        handle_divergence = ComputeShaderUtility.GetKernelHandle(StokeNavierShader, "divergence");
        handle_calculateDivergence = ComputeShaderUtility.GetKernelHandle(StokeNavierShader, "calculate_divergence_free");


        // Initialize Kernel Parameters, buffers our bound by the actual shader dispatch functions


        UpdateRuntimeKernelParameters();

        StructuredBufferToTextureShader.SetInt("_Pressure_Results_Resolution", (int)canvas_dimension);
        StructuredBufferToTextureShader.SetInt("_Velocity_Results_Resolution", (int)canvas_dimension);
        StructuredBufferToTextureShader.SetInt("_Dye_Results_Resolution", (int)canvas_dimension);
        StructuredBufferToTextureShader.SetTexture(handle_pressure_st2tx, "_Results", visualizationTexture);

        // -----------------------

        sim_command_buffer = new CommandBuffer()
        {
            name = "Simulation_Command_Buffer",
        };

        // Global Parameters that are immutable in runtime
        sim_command_buffer.SetGlobalInt("i_Resolution", (int)simulation_dimension);

    }

    // Update
    public void Tick(float deltaTime)  // should be called at same rate you wish to update your simulation, usually once a frame in update
    {
        UpdateRuntimeKernelParameters();
    }



    // ------------------------------------------------------------------
    // SIMULATION STEPS

    public void AddUserForce(ComputeBuffer force_buffer)
    {
        SetBufferOnCommandList(sim_command_buffer, force_buffer, "_user_applied_force_buffer");
        DispatchComputeOnCommandBuffer(sim_command_buffer, UserInputShader, handle_addForce, simulation_dimension, simulation_dimension, 1);
    }

    public void AddDye(ComputeBuffer dye_buffer)
    {

        SetBufferOnCommandList(sim_command_buffer, dye_buffer, "_dye_buffer");
        DispatchComputeOnCommandBuffer(sim_command_buffer, UserInputShader, handle_dye, simulation_dimension, simulation_dimension, 1);
    }

    public void Diffuse(ComputeBuffer buffer_to_diffuse)
    {

        float centerFactor = 1.0f / viscosity;
        float reciprocal_of_diagonal = viscosity / (1.0f + 4.0f * viscosity);

        sim_command_buffer.SetGlobalFloat("_centerFactor", centerFactor);
        sim_command_buffer.SetGlobalFloat("_rDiagonal", reciprocal_of_diagonal);

        bool ping_as_results = false;

        for (int i = 0; i < solver_iteration_num; i++)
        {
            ping_as_results = !ping_as_results;
            if (ping_as_results) // Ping ponging back and forth to insure no racing condition. 
            {
                SetBufferOnCommandList(sim_command_buffer, buffer_to_diffuse, "_b_buffer");
                SetBufferOnCommandList(sim_command_buffer, buffer_to_diffuse, "_updated_x_buffer");
                SetBufferOnCommandList(sim_command_buffer, FluidGPUResources.buffer_ping, "_results");
            }
            else
            {
                SetBufferOnCommandList(sim_command_buffer, FluidGPUResources.buffer_ping, "_b_buffer");
                SetBufferOnCommandList(sim_command_buffer, FluidGPUResources.buffer_ping, "_updated_x_buffer");
                SetBufferOnCommandList(sim_command_buffer, buffer_to_diffuse, "_results");
            }

            sim_command_buffer.SetGlobalInt("_current_iteration", i);
            DispatchComputeOnCommandBuffer(sim_command_buffer, SolverShader, handle_Jacobi, simulation_dimension, simulation_dimension, 1);
        }

        if (ping_as_results) // The Ping ponging ended on the helper buffer ping. Copy it to the buffer_to_diffuse buffer
        {
            Debug.Log("Diffuse Ended on a Ping Target, now copying over the Ping to the buffer which was supposed to be diffused");
            SetBufferOnCommandList(sim_command_buffer, FluidGPUResources.buffer_ping, "_Copy_Source");
            SetBufferOnCommandList(sim_command_buffer, buffer_to_diffuse, "_Copy_Target");
            DispatchComputeOnCommandBuffer(sim_command_buffer, StructuredBufferUtilityShader, handle_copyBuffer, simulation_dimension * simulation_dimension, 1, 1);
        }

        ClearBuffer(FluidGPUResources.buffer_ping, new Vector4(0.0f, 0.0f, 0.0f, 0.0f));

    }

    public void Advect(ComputeBuffer buffer_to_advect, ComputeBuffer velocity_buffer, float disspationFactor)
    {

        sim_command_buffer.SetGlobalFloat("_dissipationFactor", disspationFactor);

        SetBufferOnCommandList(sim_command_buffer, velocity_buffer, "_velocity_field_buffer");
        SetBufferOnCommandList(sim_command_buffer, buffer_to_advect, "_field_to_advect_buffer");
        SetBufferOnCommandList(sim_command_buffer, FluidGPUResources.buffer_ping, "_new_advected_field");


        DispatchComputeOnCommandBuffer(sim_command_buffer, StokeNavierShader, handle_advection, simulation_dimension, simulation_dimension, 1);

        // -------------
        SetBufferOnCommandList(sim_command_buffer, FluidGPUResources.buffer_ping, "_Copy_Source");
        SetBufferOnCommandList(sim_command_buffer, buffer_to_advect, "_Copy_Target");

        DispatchComputeOnCommandBuffer(sim_command_buffer, StructuredBufferUtilityShader, handle_copyBuffer, simulation_dimension * simulation_dimension, 1, 1);

        // -------------
        ClearBuffer(FluidGPUResources.buffer_ping, new Vector4(0.0f, 0.0f, 0.0f, 0.0f));


    }

    public void Project(ComputeBuffer buffer_to_make_divergence_free, ComputeBuffer divergence_field, ComputeBuffer pressure_field)
    {

        CalculateFieldDivergence(buffer_to_make_divergence_free, divergence_field);

        // ---------------

        float centerFactor = -1.0f;
        float diagonalFactor = 0.25f;

        sim_command_buffer.SetGlobalFloat("_centerFactor", centerFactor);
        sim_command_buffer.SetGlobalFloat("_rDiagonal", diagonalFactor);

        SetBufferOnCommandList(sim_command_buffer, divergence_field, "_b_buffer");

        ClearBuffer(pressure_field, new Vector4(0.0f, 0.0f, 0.0f, 0.0f));


        bool ping_as_results = false;

        for (int i = 0; i < solver_iteration_num; i++)
        {
            ping_as_results = !ping_as_results;
            if (ping_as_results)                     // Ping ponging back and forth to insure no racing condition. 
            {
                SetBufferOnCommandList(sim_command_buffer, pressure_field, "_updated_x_buffer");
                SetBufferOnCommandList(sim_command_buffer, FluidGPUResources.buffer_ping, "_results");
            }
            else
            {
                SetBufferOnCommandList(sim_command_buffer, FluidGPUResources.buffer_ping, "_updated_x_buffer");
                SetBufferOnCommandList(sim_command_buffer, pressure_field, "_results");
            }

            sim_command_buffer.SetGlobalInt("_current_iteration", i);
            DispatchComputeOnCommandBuffer(sim_command_buffer, SolverShader, handle_Jacobi, simulation_dimension, simulation_dimension, 1);
        }

        if (ping_as_results)                         // The Ping ponging ended on the helper buffer ping. Copy it to the buffer_to_diffuse buffer
        {
            SetBufferOnCommandList(sim_command_buffer, FluidGPUResources.buffer_ping, "_Copy_Source");
            SetBufferOnCommandList(sim_command_buffer, pressure_field, "_Copy_Target");
            DispatchComputeOnCommandBuffer(sim_command_buffer, StructuredBufferUtilityShader, handle_copyBuffer, simulation_dimension * simulation_dimension, 1, 1);
        }

        ClearBuffer(FluidGPUResources.buffer_ping, new Vector4(0.0f, 0.0f, 0.0f, 0.0f));

        // ---------------

        CalculateDivergenceFreeFromPressureField(buffer_to_make_divergence_free, pressure_field, FluidGPUResources.buffer_pong, FluidGPUResources.buffer_ping);

        SetBufferOnCommandList(sim_command_buffer, FluidGPUResources.buffer_ping, "_Copy_Source");
        SetBufferOnCommandList(sim_command_buffer, buffer_to_make_divergence_free, "_Copy_Target");
        DispatchComputeOnCommandBuffer(sim_command_buffer, StructuredBufferUtilityShader, handle_copyBuffer, simulation_dimension * simulation_dimension, 1, 1);

        ClearBuffer(FluidGPUResources.buffer_ping, new Vector4(0.0f, 0.0f, 0.0f, 0.0f));
        ClearBuffer(FluidGPUResources.buffer_pong, new Vector4(0.0f, 0.0f, 0.0f, 0.0f));
    }

    public void Visualize(ComputeBuffer buffer_to_visualize)
    {

        SetBufferOnCommandList(sim_command_buffer, buffer_to_visualize, "_Dye_StructeredToTexture_Source_RBB8");
        StructuredBufferToTextureShader.SetTexture(handle_dye_st2tx, "_Dye_StructeredToTexture_Results_RBB8", visualizationTexture);

        DispatchComputeOnCommandBuffer(sim_command_buffer, StructuredBufferToTextureShader, handle_dye_st2tx, canvas_dimension, canvas_dimension, 1);

        sim_command_buffer.Blit(visualizationTexture, BuiltinRenderTextureType.CameraTarget);

    }

    public void Visualiuse(ComputeBuffer buffer_to_visualize, Material blitMat)
    {


        SetBufferOnCommandList(sim_command_buffer, buffer_to_visualize, "_Dye_StructeredToTexture_Source_RBB8");
        StructuredBufferToTextureShader.SetTexture(handle_dye_st2tx, "_Dye_StructeredToTexture_Results_RBB8", visualizationTexture);

        DispatchComputeOnCommandBuffer(sim_command_buffer, StructuredBufferToTextureShader, handle_dye_st2tx, canvas_dimension, canvas_dimension, 1);

        sim_command_buffer.Blit(visualizationTexture, BuiltinRenderTextureType.CameraTarget, blitMat);

    }

    public void CopyPressureBufferToTexture(RenderTexture texture, ComputeBuffer buffer_to_visualize)
    {

        SetBufferOnCommandList(sim_command_buffer, buffer_to_visualize, "_Pressure_StructeredToTexture_Source_R32");
        StructuredBufferToTextureShader.SetTexture(handle_pressure_st2tx, "_Pressure_StructeredToTexture_Results_R32", texture);
        DispatchComputeOnCommandBuffer(sim_command_buffer, StructuredBufferToTextureShader, handle_pressure_st2tx, canvas_dimension, canvas_dimension, 1);

    }

    public void CopyVelocityBufferToTexture(RenderTexture texture, ComputeBuffer buffer_to_visualize)
    {

        SetBufferOnCommandList(sim_command_buffer, buffer_to_visualize, "_Velocity_StructeredToTexture_Source_RB32");
        StructuredBufferToTextureShader.SetTexture(handle_velocity_st2tx, "_Velocity_StructeredToTexture_Results_RB32", texture);
        DispatchComputeOnCommandBuffer(sim_command_buffer, StructuredBufferToTextureShader, handle_velocity_st2tx, canvas_dimension, canvas_dimension, 1);

    }


    public bool BindCommandBuffer()
    {

        main_cam.AddCommandBuffer(CameraEvent.AfterEverything, sim_command_buffer);
        return true;
    }
    // ------------------------------------------------------------------
    // HELPER FUNCTIONS

    private void CalculateFieldDivergence(ComputeBuffer field_to_calculate, ComputeBuffer divergnece_buffer)
    {

        SetBufferOnCommandList(sim_command_buffer, field_to_calculate, "_divergence_vector_field");        // Input
        SetBufferOnCommandList(sim_command_buffer, divergnece_buffer, "_divergence_values");        // Output
        DispatchComputeOnCommandBuffer(sim_command_buffer, StokeNavierShader, handle_divergence, simulation_dimension, simulation_dimension, 1);

    }

    private void CalculateDivergenceFreeFromPressureField(ComputeBuffer non_zero_vector_field, ComputeBuffer pressure_field, ComputeBuffer debug_pressure_gradient, ComputeBuffer divergence_free)
    {
        SetBufferOnCommandList(sim_command_buffer, non_zero_vector_field, "_non_zero_divergence_velocity_field");        // Input
        SetBufferOnCommandList(sim_command_buffer, pressure_field, "_pressure_field");        // Input
        SetBufferOnCommandList(sim_command_buffer, debug_pressure_gradient, "_pressure_gradient");        // Output
        SetBufferOnCommandList(sim_command_buffer, divergence_free, "_divergence_free_field");        // Output

        DispatchComputeOnCommandBuffer(sim_command_buffer, StokeNavierShader, handle_calculateDivergence, simulation_dimension, simulation_dimension, 1);
    }

    private void ClearBuffer(ComputeBuffer buffer, Vector4 clear_value)
    {
        sim_command_buffer.SetGlobalVector("_Clear_Value_StructuredBuffer", new Vector4(0.0f, 0.0f, 0.0f, 0.0f));
        SetBufferOnCommandList(sim_command_buffer, buffer, "_Clear_Target_StructuredBuffer");
        DispatchComputeOnCommandBuffer(sim_command_buffer, StructuredBufferUtilityShader, handle_clearBuffer, simulation_dimension * simulation_dimension, 1, 1);
    }

    private void SetFloatOnAllShaders(float toSet, string name)
    {
        StokeNavierShader.SetFloat(name, toSet);
        SolverShader.SetFloat(name, toSet);
        StructuredBufferToTextureShader.SetFloat(name, toSet);
        UserInputShader.SetFloat(name, toSet);
        StructuredBufferUtilityShader.SetFloat(name, toSet);
    }

    private void UpdateRuntimeKernelParameters()
    {

        SetFloatOnAllShaders(Time.time, "i_Time");

        // DYE FLUID

        float redVal = Mathf.Abs(Mathf.Sin(Time.time * 0.8f + 1.2f) + Mathf.Sin(Time.time * 0.7f + 2.0f)) / 5;
        redVal -= Mathf.Floor(redVal); //keep value in bounds

        float greenVal = Mathf.Abs(Mathf.Sin(Time.time * 0.3f + 2.0f) + Mathf.Sin(Time.time * 0.2f + 1.0f));
        greenVal -= Mathf.Floor(greenVal); //keep value in bounds

        // Use light sensor
        if (usingLightSensor)
        {
            UserInputShader.SetVector("_dye_color", new Color(redVal * lightSensor.lightSensorVal, greenVal * lightSensor.lightSensorVal, 1f * lightSensor.lightSensorVal));
        }
        else
        {
            UserInputShader.SetVector("_dye_color", new Color(redVal, greenVal, 1f));
        }

        UserInputShader.SetFloat("_mouse_dye_radius", dye_radius);
        UserInputShader.SetFloat("_mouse_dye_falloff", dye_falloff);

        // USER INPUT ADD FORCE WITH MOUSE

        float forceController = 0;

        if (Input.GetKey(forceKey)) forceController = force_strength;

        UserInputShader.SetFloat("_force_multiplier", forceController);
        UserInputShader.SetFloat("_force_effect_radius", force_radius);
        UserInputShader.SetFloat("_force_falloff", force_falloff);

        float mouse_pressed = 0.0f;




        if (Input.GetKey(dyeKey)) mouse_pressed = 1.0f;

        UserInputShader.SetFloat("_mouse_pressed", mouse_pressed);

        Vector2 mouse_pos_struct_pos = GetCurrentMouseInSimulationSpace();



        UserInputShader.SetVector("_mouse_position", mouse_pos_struct_pos); // Pass on the mouse position already in the coordinate system of the structured buffer as 2D coord
        UserInputShader.SetVector("_mouse_pos_current", mouse_pos_struct_pos); // Pass on the mouse position already in the coordinate system of the structured buffer as 2D coord
        UserInputShader.SetVector("_mouse_pos_prev", mousePreviousPos); // Pass on the mouse position already in the coordinate system of the structured buffer as 2D coord

        mousePreviousPos = mouse_pos_struct_pos;
    }

    // _______________


    private Vector2 GetCurrentMouseInSimulationSpace()
    {
        Vector3 mouse_pos_pixel_coord = Input.mousePosition;
        Vector2 mouse_pos_normalized = main_cam.ScreenToViewportPoint(mouse_pos_pixel_coord);
        mouse_pos_normalized = new Vector2(Mathf.Clamp01(mouse_pos_normalized.x), Mathf.Clamp01(mouse_pos_normalized.y));
        return new Vector2(mouse_pos_normalized.x * simulation_dimension, mouse_pos_normalized.y * simulation_dimension);
    }

    private void SetBufferOnCommandList(CommandBuffer cb, ComputeBuffer buffer, string buffer_name)
    {
        cb.SetGlobalBuffer(buffer_name, buffer);
    }

    private void DispatchComputeOnCommandBuffer(CommandBuffer cb, ComputeShader toDispatch, int kernel, uint thread_num_x, uint thread_num_y, uint thread_num_z)
    {
        DispatchDimensions group_nums = ComputeShaderUtility.CheckGetDispatchDimensions(toDispatch, kernel, thread_num_x, thread_num_y, thread_num_z);
        cb.DispatchCompute(toDispatch, kernel, (int)group_nums.dispatch_x, (int)group_nums.dispatch_y, (int)group_nums.dispatch_z);
    }
}
