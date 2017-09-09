function triPatch = convertSurfToPatch(surf) 
assert(isgraphics(surf));
assert(isequal(class(surf),'matlab.graphics.chart.primitive.Surface'));

triPatch = patch(surf2patch(surf,'triangles'));

propsToCopy = {...
	'Parent',...
	'EdgeColor',...
	'FaceColor'};
for iP = 1:length(propsToCopy)
	triPatch.(propsToCopy{iP}) = surf.(propsToCopy{iP});
end

surf.Visible = 'off';
 

end