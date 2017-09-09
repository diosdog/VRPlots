function triPatch = convertQuadPatchToTriPatch(varargin)

p = inputParser();
p.addRequired('patch',@isgraphics);
p.addParameter('doWarnIfAlreadyTri',true,@islogical);
p.parse(varargin{:});
s = p.Results;

o = s.patch;

assert(isgraphics(o));
assert(isequal(class(o),'matlab.graphics.primitive.Patch'));

if size(o.Faces,2)==3
	if s.doWarnIfAlreadyTri
		warning('Patch is made of triangle faces, not quadilateral')
	end
	% don't modify anything
	triPatch = o;
	return;
end

%TODO: add support for more edge- and face-specific properties, such as non-scalar FaceAlpha
fieldsAndAssertions = {...
	{'EdgeAlpha',@isscalar},...
	{'EdgeColor',@(x) ismember(x,{'none','flat'})},...
	{'EdgeLighting',@(x) ismember(x,{'none','flat'})},...
	{'FaceAlpha',@(x) isscalar(x)},...
	{'FaceColor',@(x) ismember(x,{'flat'}) || (isvector(x) && length(x)==3)},...
	{'FaceLighting',@(x) ismember(x,{'none','flat'})},...
	{'FaceNormals',@isempty},...
	{'FaceNormalsMode',@(x) isequal(x,'auto')},...
	{'FaceVertexAlphaData',@isempty},...
	{'FaceVertexCData',@(x) isempty(x) || (ismatrix(x) && size(x,2)==3)},...
	{'Faces',@(x) ismatrix(x) && size(x,2)==4},...
	{'VertexNormals',@isempty},...
	{'VertexNormalsMode',@(x) isequal(x,'auto')},...
	{'Vertices',@(x) ismatrix(x) && size(x,2)==3}};

for iF = 1:length(fieldsAndAssertions)
	field = fieldsAndAssertions{iF}{1};
	assertion = fieldsAndAssertions{iF}{2};
	if ~isprop(o, field)
		error('Expected property ''%s'' is not present in patch object');
	end
	
	if ~assertion(o.(field)) 
		error(['Property ''%s'' value (%s) does not comply with assumptions for quad to tri conversion.\n',...
			'May need to modify code in %s.m to properly convert this patch object.'], ...
			field, c_toString(o.(field)), mfilename);
	end
end

% note that this modified the input object (effectively) irreversibly

prevNumFaces = size(o.Faces,1);
newNumFaces = prevNumFaces*2;

newFaces = nan(newNumFaces,3);
faceMapping = nan(1,newNumFaces);
for iF = 1:prevNumFaces
	newFaces(iF*2-1,:) = o.Faces(iF,1:3);
	faceMapping(iF*2-1) = iF;
	newFaces(iF*2,:) = o.Faces(iF,[1 3:4]);
	faceMapping(iF*2) = iF;
end
o.Faces = newFaces;

if size(o.FaceVertexCData,1)==prevNumFaces
	o.FaceVertexCData = o.faceVertexCData(faceMapping,:);
end

triPatch = o;

end