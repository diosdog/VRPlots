function [extremeVals imin imax] = extrema(varargin)
% extrema: calculate min and max values simultaneously
% e.g.
%		extremeVals = extrema([1 2 3]);
% Can operate along specified dimension with 
%		extremeVals = extrema([1 2 3],[],dim)
	if nargout >= 3
		[minval, imin] = min(varargin{:});
	else
		minval = min(varargin{:});
	end
	if nargout >= 4
		[maxval, imax] = max(varargin{:});
	else
		maxval = max(varargin{:});
	end
	
	if nargin==3 && varargin{3} ~= 1
		extremeVals = [minval,maxval];
	else
		extremeVals = [minval.', maxval.'];
	end
end