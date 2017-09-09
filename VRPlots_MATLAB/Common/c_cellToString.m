function s = c_cellToString(c,varargin)
	if nargin == 0
		% test
		c_cellToString({'test',[1 2 3],'a1',{'test inner', 5}})
		return
	end
	
	if nargin > 1
		p = inputParser();
		p.addParameter('doPreferMultiline',false,@islogical);
		p.addParameter('precision',[],@isscalar);
		p.addParameter('indentation',0,@isscalar);
		p.parse(varargin{:});
		doPreferMultiline = p.Results.doPreferMultiline;
		precision = p.Results.precision;
		indentation = p.Results.indentation;
	else
		doPreferMultiline = false;
		precision = [];
		indentation = 0;
	end

	assert(iscell(c));
	
	if isempty(c)
		s = '{}';
		return;
	end
	
	s = '{';
	if doPreferMultiline
		s = [s sprintf('\t')];
	end
	assert(ndims(c)==2);
	for i=1:size(c,1)
		for j=1:size(c,2)
			if iscell(c{i,j})
				s = [s c_cellToString(c{i,j},varargin{:},'indentation',indentation+1) ','];
			elseif isnumeric(c{i,j}) && isvector(c{i,j})
				s = [s '[' num2str(c{i,j}) ']' ','];
			elseif ischar(c{i,j})
				s = [s '''' c{i,j} '''' ','];
			else
				s = [s c_toString(c{i,j},varargin{:}) ','];
				%error('unsupported type');
			end
		end
		s = s(1:end-1); % remove comma
		if i ~= size(c,1)
			s = [s ';'];
			if doPreferMultiline
				s = [s,sprintf('\n')];
				for ii = 1:indentation
					s = [s,sprintf('\t')];
				end
			end
		end
	end
	s = [s '}'];
end