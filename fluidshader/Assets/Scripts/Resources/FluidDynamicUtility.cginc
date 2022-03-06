#ifndef __FLUID_DYNAMIC_LIB__
#define __FLUID_DYNAMIC_LIB__

int   u_Resolution;
float i_grid_scale;
float i_timeStep;
float i_Time;

#define epsilon 0.000001
// Globals that need to be defined before this include is added, these are in FluidDynamicCommonUniforms

// uint u_Resolution

//1D mapping to 2D mapping of vector field
int id2Dto1D(int2 m_coord) {
    return clamp(m_coord.x, 0, u_Resolution - 1 ) + clamp(m_coord.y, 0, u_Resolution - 1 ) * u_Resolution;
}

//loads 4 closest grid centers and interpolates
float4 StructuredBufferBilinearLoad(StructuredBuffer<float4> buffer, float2 coord) 
{
    float4 closest_grid_coords;

    closest_grid_coords.xy = max(0.,round(coord - 0.5));                //get left grid centers
    closest_grid_coords.zw = closest_grid_coords.xy + float2(1., 1.);   // get right grid centers

    float2 lerp_factors    = coord - closest_grid_coords.xy;
    

    float4 left_down  = buffer[id2Dto1D(closest_grid_coords.xy)];
    float4 right_down = buffer[id2Dto1D(closest_grid_coords.zy)];
    float4 left_up    = buffer[id2Dto1D(closest_grid_coords.xw)];
    float4 right_up   = buffer[id2Dto1D(closest_grid_coords.zw)];


   return lerp(lerp(left_down, right_down, lerp_factors.x),             //x interpolation low
               lerp(left_up,   right_up,   lerp_factors.x),             //x interpolation high
               lerp_factors.y);                                         //interpolation in y direction
}

//calculates gradient of scalar field to create dye diffuse
float4 gradient(StructuredBuffer<float4> scalar_field, float partial_xy, int2 coord) {
    
    float left     = scalar_field[id2Dto1D(coord - int2(1, 0))].x;
    float right    = scalar_field[id2Dto1D(coord + int2(1, 0))].x;
    float bottom   = scalar_field[id2Dto1D(coord - int2(0, 1))].x;
    float top      = scalar_field[id2Dto1D(coord + int2(0, 1))].x;

    return float4(right - left, top - bottom, 0.0, 0.0)  / partial_xy;

}


#endif
 