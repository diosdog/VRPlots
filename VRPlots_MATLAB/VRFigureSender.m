classdef VRFigureSender < handle
	properties
		port;
		
		fieldsSupportedByAll = {...
			'Children',...
			'Position',...
			'Units',...
			'Color',...
			'Visible',...
		};
	
		% first element of each cell array is class type
		% second element of each cell array is cell array of suppported fields
		% optional third element of each cell array is modifier callback
		fieldsSupportedByClass = {...
			{'matlab.ui.Figure',{...
				'Name',...
				'Number',...
				'Colormap',...
				},...
			},...
			{'matlab.graphics.axis.Axes',{...
				'XLim',...
				'YLim',...
				...'Clim',...
				'Title',...
				'XLim',...
				'YLim',...
				'ZLim',...
				'XTick',...
				'YTick',...
				'ZTick',...
				'XTickLabel',...
				'YTickLabel',...
				'ZTickLabel',...
				...'[XYZ]Dir',...
				'XLabel',...
				'YLabel',...
				'ZLabel',...
				'DataAspectRatio',...
				},...
			},...
			{'matlab.graphics.primitive.Text',{...
				...'BackgroundColor',...
				...'Extent',...
				...'FontName',...
				'FontSize',...
				...'FontUnits',...
				...'FontWeight',...
				'HorizontalAlignment',...
				'VerticalAlignment',...
				'Rotation',...
				'String',...
				},...
			},...
			{'matlab.graphics.chart.primitive.Line',{...
				...'LineStyle',...
				...'LineWidth',...
				...'Marker',...
				...'MarkerEdgeColor',...
				...'MarkerFaceColor',...
				...'MarkerSize',...
				'XData',...
				'YData',...
				'ZData',...
				},...
			},...
			{'matlab.graphics.primitive.Line',{...
				...'LineStyle',...
				...'LineWidth',...
				...'Marker',...
				...'MarkerEdgeColor',...
				...'MarkerFaceColor',...
				...'MarkerSize',...
				'XData',...
				'YData',...
				'ZData',...
				},...
			},...
			{'matlab.graphics.chart.primitive.Scatter',{...
				'Marker',...
				'MarkerEdgeColor',...
				'MarkerFaceColor',...
				'SizeData',...
				...'LineWidth',...
				'XData',...
				'YData',...
				'ZData',...
				'CData',...
				},...
			},...
			{'matlab.graphics.chart.primitive.Surface',{...
				},...
				@(x) convertSurfToPatch(x),...
			},...
			{'matlab.graphics.primitive.Patch',{...
				...'CData',...
				'EdgeColor',...
				'FaceColor',...
				...'FaceVertexAlphaData',...
				'FaceVertexCData',...
				'Faces',...
				...'LineWidth',...
				'Vertices',...
				...'XData',...
				...'YData',...
				...'ZData',...
				},...
				@(x) convertQuadPatchToTriPatch(x,'doWarnIfAlreadyTri',false),...
			},...
			{'matlab.graphics.primitive.Group',{...
				},...
			},...
% 			{'matlab.graphics.axis.decorator.NumericRuler',{...
% 				'Limits',...
% 				...'Scale',...
% 				'TickLabels',...
% 				'TickValues',...
% 				},...
% 			},...
		};
	end

	properties(Dependent,Access=protected)
		isConnected;
	end

	properties(Access=protected)
		ni = [];
	end

	methods
		function o = VRFigureSender(varargin)
			p = inputParser();
			p.addParameter('port',21241,@isscalar);
			p.parse(varargin{:});
			s = p.Results;

			VRFigureSender.addDependencies();

			fields = fieldnames(p.Results);
			for iF = 1:length(fields)
				if isprop(o,fields{iF})
					o.(fields{iF}) = p.Results.(fields{iF});
				end
			end
		end
		
		function delete(o)
			if o.isConnected()
				o.disconnect();
			end
		end
		
		function printFigureProperties(o,fh)
			assert(ishandle(fh));
			prunedProps = o.pruneProps(fh);
			str = savejson('',prunedProps,'SingletArray',true)
			keyboard
		end
		
		function saveFigureAsJSON(o,fh,exportPath)
			assert(ishandle(fh));
			prunedProps = o.pruneProps(fh);
			savejson('',prunedProps,'SingletArray',true,'FileName',exportPath);
			c_saySingle('Wrote JSON to %s',exportPath);
		end

		function sendFigure(o,fh)
			assert(all(ishandle(fh)));
			% force units to pixels temporarily
			% (normalized units at this topmost level would require Unity to know screen size)
			if length(fh)>1
				for iF = 1:length(fh)
					c_say('Sending figure %d/%d',iF,length(fh));
					o.sendFigure(fh(iF));
					c_sayDone();
				end
				return;
			end
			prevUnits = fh.Units;
			fh.Units = 'pixels';
			prunedProps = o.pruneProps(fh);
			
			debugStr = savejson('',prunedProps,'SingleArray',true);
			
			str = savejson('',prunedProps,'SingletArray',true,'Compact',true,'SaveBinary',true);
			
			o.connect();
			assert(o.isConnected());
			
			%keyboard
			% Without SingleArray=1, arrays such as Children with only one element will not be encoded as arrays in JSON. However, setting SingleArray=1 also results in all scalar values being wrapped as an array. Due to MATLAB not using a scalar type separate from a single-element array, there doesn't seem to be a way around this

			o.ni.clearReadBuffer();
			o.ni.send('StartOfFigure');
			o.ni.send(uint32(length(str)));
			o.ni.send(str);
			o.ni.send('EndOfFigure');
			% wait for acknowledgement of receipt
			dt = 0.05;
			maxWaitTime = 30;
			ackReceived = false;
			for t=0:dt:maxWaitTime
				bytes = o.ni.tryRead();
				if ~isempty(bytes)
					% assume any message is ack message
					%c_saySingle('Read %d bytes',length(bytes));
					ackReceived = true;
					break;
				end
				pause(dt);
			end
			if ~ackReceived
				warning('Did not receive acknowledgement of receipt within %.3g s', maxWaitTime);
			end
			%c_saySingle('Returning from sendFigure');
			
			o.disconnect();
			
			fh.Units = prevUnits;
		end
		
		function isCon = get.isConnected(o)
			isCon = ~isempty(o.ni) && o.ni.isConnected;
		end
	end

	methods(Access=protected)
		function connect(o,varargin)
			p = inputParser();
			p.addParameter('timeout',30,@isscalar); % in s
			p.parse(varargin{:});
			s = p.Results;
			
			% start network server
			o.ni = c_NetworkInterfacer(...
				'port',o.port,...
				'protocol','TCP',...
				'connectionTimeout',s.timeout*1e3,...
				'isServer',false,...
				'maxSendLength',1e4);
		end

		function disconnect(o)
			if o.ni.isConnected
				c_say('Pausing before disconnecting');
				pause(1);
				c_sayDone();
				o.ni.close();
			end
			o.ni = [];
		end

		
		
		function out = pruneProps(o,props)
			if ~isstruct(props) && ~isobject(props)
				out = props;
				return;
			end
			fields = fieldnames(props);
			out = struct();
			assert(isobject(props)); % needs to be object to determine class
			out.ObjectClass = class(props);
			
			supportedFields = o.fieldsSupportedByAll;
			supportedClasses = cellfun(@(x) x{1}, o.fieldsSupportedByClass,'UniformOutput',false);
			index = find(ismember(supportedClasses,out.ObjectClass));
			if isempty(index)
				error('Unsupported class: %s',out.ObjectClass);
			end
			if length(index) > 1
				error('Duplicated class: %s',out.ObjectClass); % listed more than once in fieldsSupportedByClass
			end
			
			if length(o.fieldsSupportedByClass{index}) >= 3 && ~isempty(o.fieldsSupportedByClass{index}{3})
				% modifier callback specified for this class
				modifierCallback = o.fieldsSupportedByClass{index}{3};
				props = modifierCallback(props);
				
				% if class was changed by modifier, recursively call pruneProps
				if ~isequal(class(props),out.ObjectClass)
					out = pruneProps(o,props);
					return;
				end
			end
			
			supportedFields = union(o.fieldsSupportedByAll, o.fieldsSupportedByClass{index}{2});
			
			for iF = 1:length(fields)
				%c_say('Checking field: %s',fields{iF});
				
				if ~ismember(fields{iF},supportedFields)
					continue;
				end
			
				% if reached here, field is supported
				val = props.(fields{iF});
				if isnumeric(val) || ischar(val) || iscellstr(val)
					% no extra modification needed
					if isnumeric(val) && isempty(val)
						% savejson does some weird stuff if array is empty, so instead just don't save empty fields
						% (assuming default value on other end if no value specified will be empty)
						%TODO: change to handle this case more elegantly
					else
						out.(fields{iF}) = val;
					end
					continue;
				end
				if isobject(val)
					% recursively check children

					if isempty(fieldnames(val))
						% preserve empty struct (note that object class is not saved here)
						out.(fields{iF}) = val;
						continue;
					end

					if length(val) > 1
						% array of objects
						% check if all are the same type
						objectClasses = arrayfun(@class,val,'UniformOutput',false);
						if length(unique(objectClasses))>1
							% mix of classes in array (probably a matlab.mixin.Heterogenous array)
							% convert to an array of homogeneous graphics groups
							assert(strcmp(fields{iF},'Children'));
							groupClasses = unique(objectClasses);
							numGroups = length(groupClasses);
							hg = {};
							for iG = 1:numGroups
								hg{iG} = hggroup;
								indices = ismember(objectClasses,groupClasses{iG});
								set(val(indices),'Parent',hg{iG});
							end
							for iG = 1:numGroups
								hg{iG}.Parent = props;
								if iG==1
									tmp = o.pruneProps(hg{iG});
								else
									tmp(iG) = o.pruneProps(hg{iG});
								end
							end
							out.(fields{iF}) = tmp;
							continue;
						else
							for iS = 1:length(val) % handle homogeneous arrays of objects
								if iS==1,
									tmp = o.pruneProps(val(iS));
								else
									tmp(iS) = o.pruneProps(val(iS));
								end
							end
							val = tmp;
						end
					else
						val = o.pruneProps(val);
					end

					if isstruct(val) && ~isempty(fieldnames(val))
						% only keep obj if it had unpruned fields
						out.(fields{iF}) = val;
					end
					continue;
				end
				if iscell(val)
					if isempty(val)
						% preserve empty cell
						out.(fields{iF}) = val;
						continue;
					end
					% recursively check cell elements
					for iV = 1:length(val)
						val{iV} = o.pruneProps(val{iV});
					end
					out.(fields{iF}) = val;
					continue;
				end
				% if reached here, type is actually unsupported
				keyboard %TODO: handle other types of "supported" fields
			end
		end
	end

	methods(Static)
		function addDependencies()
			persistent pathModified;
			if isempty(pathModified)
				mfilepath=fileparts(which(mfilename));
				baseDepPath = fullfile(mfilepath,'./Common');
				%baseDepPath = '../../../Research/MATLAB/Common';
				addpath(baseDepPath);
				addpath(fullfile(baseDepPath,'Network'));

				c_NetworkInterfacer.addDependencies();

				addpath(fullfile(mfilepath,'ThirdParty','jsonlab'));
				pathModified = true;
			end
		end
		
		function RepeatedlyServeFigure(fig)
			if nargin < 1
				fig = gcf;
			end
			
			fs = VRFigureSender();
			
			while true
				try
					fs.startServing('timeout',1);
					if fs.isConnected
						c_saySingle('Just connected');
						fs.sendFigure(fig);
						while (fs.isConnected)
							c_saySingle('Looping while connected');
							pause(1);
						end
						c_saySingle('No longer connected');
					end
				catch e
					c_saySingle('Caught exception: %s',c_toString(e));
				end
				pause(1);
			end
		end
		
		function SendFigure(fig)
			if nargin < 1
				fig = gcf;
			end
			
			fs = VRFigureSender();
			
			fs.startServing('timeout',inf);
			if ~fs.isConnected
				error('Could not connect');
			end
			fs.sendFigure(fig);
		end
		
		function SaveFigureAsJSON(fig,exportPath)
			fs = VRFigureSender();
			fs.saveFigureAsJSON(fig,exportPath);
		end
		
		function ConvertFigToJSON(pathToFig,exportPath)
			VRFigureSender.addDependencies();
			
			assert(exist(pathToFig,'file')>0);
			assert(strcmpi(c_getOutputSubset(3,@fileparts,pathToFig),'.fig'));
			hf = openfig(pathToFig,'invisible');
			
			VRFigureSender.SaveFigureAsJSON(hf,exportPath);
		end
		
		function LoadAndSendFig(pathToFig)
			VRFigureSender.addDependencies();
			
			%TODO: if path is a directory, serve all .fig files in that directory
			
			assert(exist(pathToFig,'file')>0);
			assert(strcmpi(c_getOutputSubset(3,@fileparts,pathToFig),'.fig'));
			hf = openfig(pathToFig,'invisible');
			
			VRFigureSender.SendFigure(hf);
		end
	end
end