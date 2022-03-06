Final Project - Fluid Simulation
===

[Fluid Simulation Video](https://www.youtube.com/watch?v=lV8gpF6l5KM&ab_channel=JasmineDuerk)

[Light Sensor Video](https://www.youtube.com/watch?v=XtDwMuOE1IM&ab_channel=JasmineDuerk)

[Repository](https://github.com/jlduerk/Physarum)

### Explanation

For our project, we implemented a fluid simulator in Unity that simulates viscous fluid movement through the spread of dye in liquid. To drop dye into the scene, left click/drag the mouse around. To disrupt the dye and show its movement through the fluid, right click/drag. (The simulation video gets very laggy because our laptops struggle to run both OBS recording and the simulation at the same time. However, it actually runs fine normally, as can be seen in the second video taken on the phone.)

![fluid](fluid.png)

Initially, we wanted to build off of our Physarum simulation in Unity. We wanted to use the light sensor connected to an Arduino to control the simulation in an interesting way, and we were also considering using uploaded images to control the boundaries of the simulation. However, this proved to be a lot more challenging than we thought, and we pivoted to trying to create a fluid simulation. (However, we did get functionality with the light sensor working.)

Our fluid simulator uses the Navier-Stokes Equations to determine how the fluid moves when disrupted by mouse input. It reads mouse inputs to determine when dye is placed and when the fluid is moved. Once dye is placed (left-click), it begins to fade into the liquid. When the dye is moved (right-click), the code begins the process of diffusion, advection, and projection. The movement of the dye is determined using these functions in addition to the equation and is affected by set velocity, viscosity, and input force. Because of the concept of our simulation, our theme was water, and we colored the dye accordingly.

Along with attempting to implement a new simulation of our choosing, we also experimented with unconventional interaction sources using a light sensor. Our project currently has a separate script reading in sensor values, and the simulation manager has a boolean to turn the light sensor on and off. The light sensor then controls the size and brightness of the dye being added. We also wanted it to control the diffusion rates, but our parameter for that option did not really work.

Some of the challenges we faced with the fluid simulation were sensor based, and some we are still unsure of the issue. The first challenge was that the light sensor was unable to run on Karen’s laptop, and would freeze the program when run. The sensor failed to connect, even in the Arduino app, and no input stream was detected. As such, only Jasmine could test any code using the light sensor. Another challenge was that the light sensor was incredibly slow in detecting changes. This was addressed by increasing the delay between readings, so program did not get stuck trying to parse readings from a long time ago. A final challenge with the simulation was that it stopped running normally after one of our pushes. When opening the Unity project, we have to place an older version of the FluidSimulation.cs script in, resolve all errors, run it, and then undo and repaste the current FluidSimulation.cs script back in for the simulation to run as it should. We are unsure why this happens, but we think it has something to do with settings getting deleted on load. In general, the simulation was very challenging to implement, especially because we had to understand how shaders work in the Unity engine.

Reference: 
https://shahriyarshahrabi.medium.com/gentle-introduction-to-fluid-simulation-for-programmers-and-technical-artists-7c0045c40bac



