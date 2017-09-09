classdef c_NetworkInterfacer < handle
	properties
		IP;
		port;
		protocol;
		con = [];
		method;
		doDebug;
		isServer;
		connectionTimeout;
		maxSendLength;
		callback_keepTryingToConnect;
		callback_bufferOverflow;
	end
	
	properties(Dependent,SetAccess=protected)
		isTCP;
		isUDP;
	end
	
	properties(Access=protected)
		jtcpBufferLength;
		jtcpDoUseHelperClass;
		jtcpHelperClassPath = '';
	end
	
	methods
		%% constructor
		function o = c_NetworkInterfacer(varargin)
			c_NetworkInterfacer.addDependencies();
			
			p = inputParser();
			p.addParameter('IP','127.0.0.1',@ischar);
			p.addParameter('port',5555,@isscalar);
			p.addParameter('protocol','TCP',@(x) ischar(x) && ismember(x,{'UDP','TCP'}));
			p.addParameter('tcp_doUsePnet',false,@islogical);
			p.addParameter('doDebug',false,@islogical);
			p.addParameter('jtcpBufferLength',5e3,@isscalar);
			p.addParameter('jtcpDoUseHelperClass',false,@islogical);
			p.addParameter('isServer',false,@islogical);
				% if isServer, listens for incoming connections from other IPs
				% else if ~isServer, tries to connect to remote server at specified IP
			p.addParameter('connectionTimeout',1e3,@isscalar); % in ms
			p.addParameter('callback_keepTryingToConnect',[],@(x) isempty(x) || isa(x,'function_handle')); 
				% called periodically if connectionTimeout==inf; if returns false, will abort connection attempt
			p.addParameter('callback_bufferOverflow',@() warning('buffer overflow detected'), @(x) isempty(x) || isa(x,'function_handle'));
			p.addParameter('maxSendLength',inf,@isscalar);
			p.parse(varargin{:});
			
			% copy parameters to class properties with the same names
			for iP = 1:length(p.Parameters)
				if isprop(o,p.Parameters{iP})
					o.(p.Parameters{iP}) = p.Results.(p.Parameters{iP});
				end
			end
		
			switch(o.protocol)
				case 'UDP'
					o.method = 'c_judp';
				case 'TCP'
					if p.Results.tcp_doUsePnet
						o.method = 'pnet-tcp';
					else
						o.method = 'jtcp';
					end
				otherwise
					error('Invalid protocol');
			end
			
			if strcmpi(o.method,'jtcp') && o.jtcpDoUseHelperClass
				% look for java helper class
				path = which('jtcp');
				dir = fileparts(path);
				helperPath = fullfile(dir,'Java','DataReader.class');
				if exist(helperPath,'file')
					% found helper class
					o.jtcpHelperClassPath = fileparts(helperPath);
				else
					warning('jtcp helper class not located at %s. Not using.',helperPath)
					o.jtcpDoUseHelperClass = false;
					o.jtcpHelperClassPath = '';
				end
			end
			
			o.connect();
		end
		
		%% destructor
		function delete(o)
			o.close();
		end
		%% connection
		function connect(o)
			switch(o.method)
				case 'jtcp'
					try
						% if timeout is a multiple of 1 s, break into smaller chunks to allow interruption
						shorterTimeout = 1e3;
						doKeepTrying = true;
						if doKeepTrying && (o.connectionTimeout > shorterTimeout && (isinf(o.connectionTimeout) || mod(o.connectionTimeout,shorterTimeout)==0))
							numRepeats = o.connectionTimeout / shorterTimeout;
							counter = 0;
							o.con = [];
							
							while counter < numRepeats && isempty(o.con)
								try
									counter = counter + 1;
									if o.isServer
										o.con = jtcp('accept', o.port,...
											'serialize',false,...
											'timeout',shorterTimeout,...
											'receiveBufferSize',o.jtcpBufferLength);
									else
										o.con = jtcp('request', o.IP, o.port,...
											'serialize',false,...
											'timeout',shorterTimeout,...
											'receiveBufferSize',o.jtcpBufferLength);
									end
								catch e
									if ~(o.isServer && strcmp(e.identifier,'jtcp:connectionAcceptFailed')) && ...
											~(~o.isServer && strcmp(e.identifier,'jtcp:connectionRequestFailed')) 
										rethrow(e)
									end
									drawnow; % to allow processing of queued events
									if ~isempty(o.callback_keepTryingToConnect)
										if ~o.callback_keepTryingToConnect()
											% abort connection attempt early
											o.con = [];
											warning('Aborting connection attempt.');
											doKeepTrying = false;
											break;
										end
									end
								end
							end
						else
							if o.isServer
								o.con = jtcp('accept', o.port,...
										'serialize',false,...
										'timeout',o.connectionTimeout,...
										'receiveBufferSize',o.jtcpBufferLength);
							else
								o.con = jtcp('request', o.IP, o.port,...
									'serialize',false,...
									'timeout',o.connectionTimeout,...
									'receiveBufferSize',o.jtcpBufferLength);
							end
						end

					catch e
						if strcmp(e.identifier,'jtcp:connectionRequestFailed') || strcmp(e.identifier,'jtcp:connectionAcceptFailed')
							warning('Failed to connect. Is server running?');
							o.con = [];
						else
							rethrow(e)
						end
					end
				case 'pnet-tcp'
					if o.connectionTimeout < inf
						error('Connection timeout not currently supported for %s',o.method);
					end
					
					if ~isempty(o.callback_keepTryingToConnect)
						error('keepTryingToConnect callback not currently supported for %s',o.method);
					end
					
					o.con = pnet('tcpconnect', o.IP, o.port);
					% Check established connection and display a message
					stat = pnet(o.con,'status');
					if stat <= 0
						warning('Failed to connect. Is server running?');
						o.con = [];
					end
				
				case 'judp'
					% nothing to connect
					
					if o.connectionTimeout < inf
						error('Connection timeout not currently supported for %s',o.method);
					end
					
					if ~isempty(o.callback_keepTryingToConnect)
						error('keepTryingToConnect callback not currently supported for %s',o.method);
					end
					
				case 'c_judp'
					if ~isempty(o.callback_keepTryingToConnect)
						error('keepTryingToConnect callback not currently supported for %s',o.method);
					end
					
					if o.isServer
						o.con = c_judp(...
							'port',o.port,...
							'receiveTimeout',o.connectionTimeout);
					else
						o.con = c_judp(...
							'IP',o.IP,...
							'port',o.port,...
							'canReceive',false);
					end
					
				otherwise
					error('Invalid method');
			end
			if ~isempty(o.con)
				if o.doDebug
					c_saySingle('Connection to %s:%d successful',o.IP,o.port);
				end
			end
		end
		
		function iscon = isConnected(o)
			switch(o.method)
				case 'judp'
					iscon = true; % since udp doesn't maintain open connection, just return always connected
				case 'c_judp'
					iscon = ~isempty(o.con) && o.con.isConnected();
				otherwise
					iscon = ~isempty(o.con);
			end
		end
		
		function close(o)
			if ~isempty(o.con)
				switch(o.method)
					case 'jtcp'
						jtcp('close',o.con);
					case 'pnet-tcp'
						pnet('closeall'); %TODO: probably replace this with a more specific call to avoid closing other objects' connections
					case 'judp'
						% nothing to close for judp
					case 'c_judp'
						o.con.close();
					otherwise
						error('Invalid method');
				end	
				o.con = [];
			end
			if o.doDebug
				c_saySingle('Connection closed');
			end
		end
		
		
		%% receiving 
		function bytesRead = tryRead(o,varargin)
			p = inputParser();
			p.addParameter('numBytes',[],@(x) isscalar(x) || isempty(x));
			p.addParameter('maxNumBytes',[],@(x) isscalar(x) || isempty(x));
			p.addParameter('doBlock',false,@islogical);
			p.addParameter('convertTo','',@ischar);
			p.parse(varargin{:});
			s = p.Results;
			
			bytesRead = [];
			
			switch(o.method)
				case 'jtcp'
					if s.doBlock
						keyboard %TODO: implement blocking 
					end
					numAvailableBytes = o.con.socketInputStream.available;
					if numAvailableBytes == o.jtcpBufferLength
						if ~isempty(o.callback_bufferOverflow)
							o.callback_bufferOverflow();
						end
						numAvailableBytes = o.con.socketInputStream.available; % re-run in case callback cleared buffer
					end

					if isempty(s.numBytes) || numAvailableBytes >= s.numBytes
						args = {};
						if ~isempty(s.maxNumBytes)
							args = [args,'MAXNUMBYTES',s.maxNumBytes];
						elseif ~isempty(s.numBytes)
							args = [args,'NUMBYTES',s.numBytes];
						end
						if o.jtcpDoUseHelperClass
							args = [args,'helperClassPath',o.jtcpHelperClassPath];
						end
						bytesRead = jtcp('READ',o.con, args{:});
					else
						bytesRead = [];
					end
				case 'pnet-tcp'
					keyboard %TODO
				case 'judp'
					if isempty(s.numBytes) && ~isempty(s.maxNumBytes)
						numBytes = s.maxNumBytes;
					elseif ~isempty(s.numBytes) && isempty(s.maxNumBytes)
						numBytes = s.numBytes;
					elseif ~isempty(s.numBytes) && ~isempty(s.maxNumBytes)
						numBytes = min(s.numBytes,s.maxNumBytes);
					else
						error('Must specify either numBytes or maxNumBytes');
					end

					if s.doBlock
						timeout = s.connectionTimeout;
					else
						timeout = 0;
					end

					% judp does not allow timeouts of exactly zero
					if timeout == 0
						timeout = 1;
					end

					try
						[bytesRead, sourceIP] = judp('RECEIVE',o.port,numBytes,timeout);
					catch e
						if strcmpi(e.identifier,'judp:timeout')
							bytesRead = [];
						else
							rethrow(e);
						end
					end

					bytesRead = bytesRead';

					if o.doDebug
						c_saySingle('JUDP received %d bytes from IP %s',length(bytesRead), sourceIP);
					end

				case 'c_judp'
					if isempty(s.numBytes) && ~isempty(s.maxNumBytes)
						numBytes = s.maxNumBytes;
					elseif ~isempty(s.numBytes) && isempty(s.maxNumBytes)
						numBytes = s.numBytes;
					elseif ~isempty(s.numBytes) && ~isempty(s.maxNumBytes)
						numBytes = min(s.numBytes,s.maxNumBytes);
					else
						error('Must specify either numBytes or maxNumBytes');
					end

					if s.doBlock
						timeout = o.connectionTimeout;
					else
						timeout = 0;
					end

					[bytesRead, sourceIP ] = o.con.tryRead(...
						'maxNumBytes',numBytes,...
						'timeout',timeout);

				otherwise
					error('Invalid method');
			end

			if ~isempty(s.convertTo)
				if strcmpi(s.convertTo,'char')
					bytesRead = char(bytesRead);
				else
					bytesRead = typecast(bytesRead,s.convertTo);
				end
			end
		end
		
		function numBytes = numBytesAvailable(o)
			switch(o.method)
				case 'jtcp'
					numBytes = o.con.socketInputStream.available;
				otherwise
					error('unsupported');
			end
		end
		
		function clearReadBuffer(o)
			switch(o.method)
				case 'jtcp'
					startNumBytes = o.con.socketInputStream.available;
					numBytesToRead = startNumBytes;
					while numBytesToRead > 0
						args = {};
						args = [args,'NUMBYTES',numBytesToRead];
						if o.jtcpDoUseHelperClass
							args = [args,'helperClassPath',o.jtcpHelperClassPath];
						end
						bytesRead = jtcp('READ',o.con, args{:});
						numBytesToRead = numBytesToRead - length(bytesRead);
					end
				otherwise
					error('Unsupported');
			end
		end
		
		%% sending
		function send(o,toSend)
			assert(iscell(toSend) || isvector(toSend));
			
			if ~iscell(toSend)
				toSend = {toSend};
			end
			
			% crude serialization
			for i = 1:length(toSend)
				if ischar(toSend{i})
					o.sendBytes(int8(toSend{i}));
				else
					for j = 1:length(toSend{i})
						assert(isscalar(toSend{i}(j)));
						o.sendBytes(typecast(toSend{i}(j),'int8'));
					end
				end
			end
		end
		
		function sendBytes(o,bytesToSend)
			assert(isa(bytesToSend,'int8') || isa(bytesToSend,'uint8'));
			assert(o.isConnected());
			switch(o.method)
				case 'jtcp'
					if length(bytesToSend) > o.maxSendLength
						numChunks = ceil(length(bytesToSend)/o.maxSendLength);
						for iC = 1:numChunks
							jtcp('write',o.con,bytesToSend((1+(iC-1)*o.maxSendLength):min(iC*o.maxSendLength,length(bytesToSend))));
						end
					else
						jtcp('write',o.con,bytesToSend);
					end
					
				case 'pnet-tcp'
					keyboard %TODO
					
				case 'judp'
					%TODO: add support for o.maxSendLength as above
					judp('SEND',o.port,o.IP, bytesToSend)
					
				case 'c_judp'
					%TODO: add support for o.maxSendLength as above
					o.con.sendBytes(bytesToSend);
					
				otherwise
					error('Invalid method');
			end
		end
		
		%% getters/setters
		function isTCP = get.isTCP(o)
			isTCP = strcmpi(o.protocol,'TCP');
		end
		
		function isUDP = get.isUDP(o)
			isUDP = strcmpi(o.protocol,'UDP');
		end
				
	end
	
	properties(Access=protected, Constant)
		test_port = 51241;
		test_msg = 'Test message';
	end
	
	methods(Static)
		function addDependencies
			persistent pathModified;
			if isempty(pathModified)
				mfilepath=fileparts(which(mfilename));
				addpath(fullfile(mfilepath,'../ThirdParty/judp'));
				addpath(fullfile(mfilepath,'../ThirdParty/jtcp'));
				addpath(fullfile(mfilepath,'../ThirdParty/pnet'));
				pathModified = true;
			end
		end
		
		function testFn_jtcp()
			keyboard %TODO
		end
		
		function testFn_judp_tx()
			port = c_NetworkInterfacer.test_port;
			msg = c_NetworkInterfacer.test_msg;
			
			nctx = c_NetworkInterfacer(...
				'port',port,...
				'protocol','UDP');
			
			c_saySingle('JUDP_Tx test waiting to start. Press any key to continue');
			pause
			while true
				nctx.send(msg);
				c_saySingle('Sent test message ''%s''. Press ctrl-c to terminate, or any other key to send again.',msg);
				pause
			end
		end
		
		function testFn_judp_rx()
			port = c_NetworkInterfacer.test_port;
			nctx = c_NetworkInterfacer(...
				'port',port,...
				'protocol','UDP',...
				'isServer',true);
			
			expectedMsg = c_NetworkInterfacer.test_msg;
			
			c_saySingle('JUDP_Rx test starting infinite read loop. Press ctrl-c to terminate.');
			while true
				msg = nctx.tryRead('numBytes',length(expectedMsg),'convertTo','char','doBlock',true);
				if isequal(msg,expectedMsg)
					c_saySingle('Received expected message: ''%s''',msg);
				elseif ~isempty(msg)
					warning('Received unexpected message: ''%s''',msg);
					keyboard
				else
					c_saySingle('Timed out before receiving message');
				end
				pause(0.001);
			end
		end
			
	end
end