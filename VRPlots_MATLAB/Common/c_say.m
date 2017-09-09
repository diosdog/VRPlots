function c_say(varargin)
	global sayNestLevel;
	if isempty(sayNestLevel)
		sayNestLevel = 0;
	end
	
	global saySilenceLevel;
	if ~isempty(saySilenceLevel) && sayNestLevel >= saySilenceLevel
		% don't print anything
		sayNestLevel = sayNestLevel + 1;
		return
	end
	
	global sayDateFormat;
	if isempty(sayDateFormat)
		sayDateFormat = 'HH:MM:ss';
	end
	
	if verLessThan('matlab','8.4')
		fprintf('%s ',datestr(now,13));
	else
		fprintf('%s ',datestr(datetime,sayDateFormat));
	end

	for i=1:sayNestLevel
		if mod(i,2)==0
			fprintf(' ');
		else
			fprintf('|');
		end
	end
	sayNestLevel = sayNestLevel + 1;
	fprintf('v ');
	fprintf(varargin{:});
	fprintf('\n');
end