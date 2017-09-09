# VRPlots
View MATLAB-generated plots in virtual reality!

## Usage

Launch VRPlots_Unity.exe.

In MATLAB:
    
    figure; 
    [X,Y,Z] = peaks(25); 
    surf(X,Y,Z);
    VRFigureSender.SendFigure();

Or try a series of demo figures:

    DemoVRPlots();

![](doc/Example.gif)
