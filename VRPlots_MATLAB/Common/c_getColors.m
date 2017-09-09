function colors = c_getColors(n_colors,varargin)
% wrapper around third-party distinguishable_colors()

persistent PathModified;
if isempty(PathModified)
	mfilepath=fileparts(which(mfilename));
	addpath(fullfile(mfilepath,'./ThirdParty/distinguishable_colors'));
	PathModified = true;
end

colors = distinguishable_colors(n_colors,varargin{:});

end