function DemoVRPlots()

close all

persistent pathModified;
if isempty(pathModified) || ~pathModified
	mfilepath=fileparts(which(mfilename));
	addpath(fullfile(mfilepath,'./Common'));
	pathModified = true;
end

fh = gobjects(0);
for i=1:1
	fh(end+1) = figure('name','2D plot');			examplePlot_simple2D();
	fh(end+1) = figure('name','3D plot');			examplePlot_simple3D();
	fh(end+1) = figure('name','subplot example');	examplePlot_subplot();
 	fh(end+1) = figure('name','3D scatter plot');	examplePlot_scatter3();
	fh(end+1) = figure('name','3D mixed plot');		examplePlot_scatterPlusLines();
	fh(end+1) = figure('name','Surf plot');			examplePlot_surf();
	fh(end+1) = figure('name','Patch plot');		examplePlot_patch();
end

% one-time connect and send
fs = VRFigureSender();
fs.sendFigure(fh);

end

function examplePlot_simple2D()
	N = 10;
	x = 1:N;
	y = cumsum(rand(1,N)*2);
	plot(x,y,'-o');
	hold on;
	plot(x,y*2,'-.');
	%set(gca,'DataAspectRatio',[1 1 1]);
end

function examplePlot_simple3D()
	t = 0:pi/50:10*pi;
	st = sin(t);
	ct = cos(t);
	plot3(st,ct,t);
	title('Helix');
	xlabel('x axis');
	ylabel('y axis');
	zlabel('z axis');
end

function examplePlot_subplot()
	subplot(1,2,1);
	examplePlot_simple2D();
	subplot(1,2,2);
	examplePlot_simple3D();
end

function examplePlot_scatter3()
	[X,Y,Z] = sphere(12);
	x = [0.5*X(:); 0.75*X(:); X(:)];
	y = [0.5*Y(:); 0.75*Y(:); Y(:)];
	z = [0.5*Z(:); 0.75*Z(:); Z(:)];
	
	S = repmat([70,50,20],numel(X),1);
	C = repmat([1,2,3],numel(X),1);
	s = S(:);
	c = C(:);
	
	if 1
		scatter3(x,y,z,s,c,'filled');
	else
		colors = get(gca,'ColorOrder');
		c_plot_scatter3([x,y,z],...
			'ptColors',colors(mod(c-1,size(colors,1))+1,:),...
			'ptSizes',s/1000);
	end
	axis equal
	view(3);
	
end

function examplePlot_scatterPlusLines()
	N = 3;
	pts = rand(N*2,3)*100;
	
	c_plot_scatter3(pts,...
		'ptColors',repmat(c_getColors(N*2),1,1),...
		'sphereN',8);
	
	hold on;
	args = c_mat_sliceToCell(pts,2);
	line(args{:});
	
	
	view(3);
	
	axis equal
end

function examplePlot_surf() 
	[X,Y,Z] = peaks(25);
	
	surf(X,Y,Z);

end

function examplePlot_patch()
	s = surf(peaks(24));
	patch(surf2patch(s,'triangles'));
	delete(s);
	shading faceted;
end