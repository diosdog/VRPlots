function output = c_mat_sliceToCell(mat,dim)
% slice a matrix up into pieces and return each piece as an element in a cell array
% (e.g. for constructing x,y,z input to scatter3 from a single list of 3d coordinates)
if nargin < 2
	dim = c_findFirstNonsingletonDimension(mat);
end

assert(isnumeric(mat)); % not actually necessary, could be removed 

numSlices = size(mat,dim);
output = cell(1,numSlices);
permOrder = 1:ndims(mat);
permOrder = circshift(permOrder,-dim+1,2);
mat = permute(mat,permOrder);
origSize = size(mat);
mat = reshape(mat,origSize(1),prod(origSize(2:end)));
for i=1:numSlices
	sliceMat = mat(i,:);
	sliceMat = reshape(sliceMat,[1, origSize(2:end)]);
	sliceMat = ipermute(sliceMat,permOrder);
	output{i} = sliceMat;
end
end