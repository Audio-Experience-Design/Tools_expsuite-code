function AA_GenerateSOFA(sofaname,workdir,settingsfile,itemlistfile,referencefile,doplots,saveRaw,saveWin,saveEQ,saveEQmp,saveITD,targetFs,removeRedundant)

% Process recorded sweeps into SOFA files.
% This replaces all the VisualBasic code from the IRToolbox, and can be run
% from Matlab directly
% NOTE: this script will always remove redundant positions (edited by
% Rapolas Daugintis, 2 May 2024). Also, the 1 ms padding that caused
% negative delays changed to 0.75ms padding.
removeRedundant = 1;

r = 1.5; % TODO: verify this is the correct distance from speaker driver to arc center
gain = 20; % in dB; TODO: input this as a parameter

% To save figures
figdir = [workdir,'/plots'];
if ~isfolder(figdir)
    mkdir(figdir)
end

%% Load settings
settings = AA_ReadSettingsFile(settingsfile);
fs = settings.fs;
isdList = settings.isdList;
srcList = settings.srcList;
irLen = round(settings.irLen*fs/1e3); % in samples
irOffset = round(settings.irOffset*fs/1e3); % in samples
clear settings

%% Window settings
rawlen = 0.05 * fs; % for the raw HRIRs we keep 50 ms
fadelenin = 16;
fadelenout = 128;
tin = linspace(0,pi/2,fadelenin).';
tout = linspace(0,pi/2,fadelenout).';
fadein = sin(tin).^2;
fadeout = cos(tout).^2;
win = [fadein; ones(irLen-fadelenin-fadelenout,1); fadeout];

%% Load sweep files
sweepfile = [workdir,'/expsweep.wav'];
[sweep,fs_] = audioread(sweepfile);
assert(fs==fs_,'Sampling frequency mismatch!')
invsweepfile = [workdir,'/invexpsweep.wav'];
[invsweep,fs_] = audioread(invsweepfile);
assert(fs==fs_,'Sampling frequency mismatch!')

%% Normalise inverse sweep so that the deconvolution has 0dB magnitude
swlen = size(invsweep,1);
nfft = 2^nextpow2(swlen);
tmp = fft(sweep,nfft).*fft(invsweep,nfft);
amp = abs(tmp(round(nfft*1000/fs))); % normalize at 1khz
invsweep = invsweep./amp;
clear tmp amp

%% Prepare inter-sweep delay list
ISD = zeros(size(isdList));
for i=1:numel(isdList)
    ISD(i) = sum(isdList(1:i))*fs/1000; % in samples
end

%% Calculate HRIRs
itemlist = readtable(itemlistfile,'Delimiter',',');
indAz = find(~isnan(itemlist.Azimuth)); % ignore rows without defined azimuth
numAz = numel(indAz);
count = 1;
pos = [];
h = [];
for i=1:numAz
    row = indAz(i);
    az = mod(360-itemlist.Azimuth(row),360); % source az = - turntable az
    id = itemlist.Index(row);
    el = str2num(itemlist.Elevation{row});
    numEl = numel(el);
    % Deconvolve
    ir = [];
    for ch=1:2
        file = sprintf('%s/AMTatARI_%0.4d_adc%d.wav',workdir,id,ch-1);
        if ~isfile(file)
            continue
        end
        [x,fs_] = audioread(file); % read recording
        if max(abs(x(:))) >= 0.99
            warning('The microphone clipped for Az=%d! Consider reducing the microphone gain and repeating the measurement.',itemlist.Azimuth(row))
        end
        assert(fs==fs_,'Sampling frequency mismatch!')
        convlen = size(x,1) + swlen - 1;
        nfft = 2^nextpow2(convlen);
        irtemp = ifft( fft(x,nfft) .* fft(invsweep,nfft) );
        ir(:,ch) = irtemp(1:convlen);
    end
    % If files were not found, skip this azimuth
    if size(ir,2) < 2
        continue
    end
    
    % Separate HRIRs
    for j=1:numEl
        ind = srcList(:,2)==el(j);
        lat(j) = srcList(ind,3)*fs/1e6; % in samples
        % NOTE: in this version, the initial offset is counted as part of
        % the IR length. The end offset is not used.
        ibeg=int32(swlen+(ISD(j))+lat(j)-irOffset(1));
        %iend=int32(swlen+(ISD(j))+lat(j)+irLen+irOffset(2)-1);
        iend = ibeg + rawlen - 1;
        for ch=1:2
            h(:,count,ch)=ir(ibeg:iend,ch);
        end
        pos(count,:) = [az,el(j),r];
        count = count+1;
    end
end

if size(h,2) == 0
    warning('No HRTF recordings were found.')
    return
elseif size(h,2) < numAz*numEl
    warning('Some azimuths were skipped because the corresponding HRTF recordings were not found.')
end

% Apply gain
h = h.*db2mag(gain);

%% Check that IR peak has a healthy SNR above noise floor
SNRthresh = 60; % TODO; pass as parameter
pad = round(0.0005 * fs); % look for the floor at 0.5ms before onset (empirically set)
[peakL,onsL] = max(db(abs(h(:,:,1))),[],1);
[peakR,onsR] = max(db(abs(h(:,:,2))),[],1);
floorL = mean(db(abs(h(1:onsL-pad,:,1))),1);
floorR = mean(db(abs(h(1:onsR-pad,:,2))),1);
if any((peakL-floorL) < SNRthresh) || any((peakR-floorR) < SNRthresh)
    figure('pos',[12 91 1065 420])
    subplot(1,2,1), dummyplot = plot([nan nan],[nan nan],'k--','LineWidth',2); hold on
    AKp(h(1:round(0.003*fs),:,1),'et2d','fs',fs), hold on
    plot([0,3],[mean(peakL-SNRthresh),mean(peakL-SNRthresh)],'k--','LineWidth',2)
    title('Left'), legend(dummyplot,{'Noise floor should be below this'},'location','se')
    subplot(1,2,2), dummyplot = plot([nan nan],[nan nan],'k--','LineWidth',2); hold on
    AKp(h(1:round(0.003*fs),:,2),'et2d','fs',fs), hold on
    plot([0,3],[mean(peakR-SNRthresh),mean(peakR-SNRthresh)],'k--','LineWidth',2)
    title('Right'), legend(dummyplot,{'Noise floor should be below this'},'location','se')
    sgtitle('Warning: some HRIRs display a high noise floor before the onset')
    warning('The noise floor before the onset is very high for some HRIRs (see figure). Please check the microphones')
end

%% Remove redundant directions
az = pos(:,1);
el = pos(:,2);
[lat,pol] = sph2hor(az,el);
if removeRedundant
    [~,ind] = unique([lat,pol],'rows');
    ind = sort(ind);
    h = h(:,ind,:);
    az = az(ind);
    el = el(ind);
    pos = pos(ind,:);
end
ndirs = size(h,2);

%% Window HRIRs
hwin = win.*h(1:irLen,:,:);
% Check energy loss
nrg = sum(abs(h(:)).^2);
nrgwin = sum(abs(hwin(:)).^2);
nrgloss = 1-nrgwin/nrg;
if nrgloss>0.02 % TODO: improve
    warning('%0.2f%% of the IR energy was lost after windowing. Maybe something went wrong. Please check plots.',nrgloss*100)
    ears = {'Left','Right'};
    figure('pos',[12 91 1357 785])
    for ch=1:2
        subplot(2,1,ch)
        AKp(h(:,:,ch),'et2d','fs',fs), hold on 
        % Plot window and show title
        yyaxis('right')
        plot([(0:irLen-1)*1000/fs],win,'r-.','LineWidth',1.5)
        ax = gca; ax.YAxis(2).Color = 'r';
        ylabel('Window amplitude')
        title(sprintf('%s',ears{ch}))
    end
    sgtitle('Warning: large energy loss when windowing HRIRs. The red window should cover the first few ms of the HRIR.')
end

%% Equalise by reference measurement

if saveEQ 

    s = load(referencefile);
    ref_el = s.el;
    ref_eq = s.eq;
    ref_delay = s.delay;
    clear s
    
    convlen = irLen+size(ref_eq,1)-1;
    nfft = 2^nextpow2(convlen);
    safety = 64; % 64 samples at 96kHz is around 0.67 ms which is enough to cover the head diameter and some more

    heq = zeros(irLen,ndirs,2);
    count = 0;
    show_warning = 0;
    for i=1:numel(ref_el) 
        ind = ref_el(i) == el;
        count = count + sum(ind);
        tmp = iffth( ffth(hwin(:,ind,:),nfft) .* ffth(ref_eq(:,i,:),nfft) );
        tmp = circshift(tmp,-ref_delay+safety);
        heq(:,ind,:) = win.*tmp(1:irLen,:,:);

        % Check energy loss
        nrg = sum(abs(tmp).^2,'all');
        nrgwin = sum(abs(heq(:,ind,:)).^2,'all');
        nrgloss = 1-nrgwin/nrg;
        if nrgloss>0.02
            show_warning = 1;
            warning('%0.2f%% of the IR energy was lost after windowing. Maybe something went wrong. Please check plots.',nrgloss*100)
            figure
            subplot(2,2,1), AKp(tmp(:,:,1),'et2d','fs',fs), title('Before window L')
            subplot(2,2,2), AKp(tmp(:,:,2),'et2d','fs',fs), title('Before window R')
            subplot(2,2,3), AKp(heq(:,ind,1),'et2d','fs',fs), title('After window L')
            subplot(2,2,4), AKp(heq(:,ind,2),'et2d','fs',fs), title('After window R')
            sgtitle(sprintf('el=%d',ref_el(i)))
        end
    end
    if show_warning == 1
        warning('More than 2% of the HRTF energy was lost when windowing after equalisation. Maybe something went wrong.')
    end

end

%% Equalise by reference measurement (minimum phase version)

if saveEQmp
    
    s = load(referencefile);
    ref_el = s.el;
    ref_eqmp = s.eqmp;
    clear s
    
    convlen = irLen+size(ref_eqmp,1)-1;
    nfft = 2^nextpow2(convlen);

    heqmp = zeros(irLen,ndirs,2);
    count = 0;
    show_warning = 0;
    for i=1:numel(ref_el) 
        ind = ref_el(i) == el;
        count = count + sum(ind);
        tmp = iffth( ffth(hwin(:,ind,:),nfft) .* ffth(ref_eqmp(:,i,:),nfft) );
        heqmp(:,ind,:) = win.*tmp(1:irLen,:,:);

        % Check energy loss
        nrg = sum(abs(tmp).^2,'all');
        nrgwin = sum(abs(heq(:,ind,:)).^2,'all');
        nrgloss = 1-nrgwin/nrg;
        if nrgloss>0.02
            show_warning = 1;
            warning('%0.2f%% of the IR energy was lost after windowing. Maybe something went wrong. Please check plots.',nrgloss*100)
            figure
            subplot(2,2,1), AKp(tmp(:,:,1),'et2d','fs',fs), title('Before window L')
            subplot(2,2,2), AKp(tmp(:,:,2),'et2d','fs',fs), title('Before window R')
            subplot(2,2,3), AKp(heq(:,ind,1),'et2d','fs',fs), title('After window L')
            subplot(2,2,4), AKp(heq(:,ind,2),'et2d','fs',fs), title('After window R')
            sgtitle(sprintf('el=%d',ref_el(i)))
        end
    end  
    
    if show_warning == 1
        warning('More than 2% of the HRTF energy was lost when windowing after min-phase equalisation. Maybe something went wrong.')
    end
    
end


%% Ensure that all elevations are between -90 and 90
ind = el>90;
az(ind) = mod(az(ind)+180,360); % azimuth is inverted (turntable =/= speakers)
el(ind) = 180-el(ind);
pos(:,1) = az;
pos(:,2) = el;

%% Perform ALFE

% NOTE: not using it for now since it has a very small effect

%In this section the lower frequency components below a certain frequency
%xover are replaced by a frequecy-shaped dirac. The ALFE algorithm is done
%on the equalized dataset in order to benefit from the constant frequency
%response towards low frequencies. 

% xover = [200 300];
% nfreqs = size(Heq,1);
% n_link = round(xover / (fs/nfreqs)) + 1;
% ALFE_abs = db(mean(abs(Heq(n_link(1):n_link(2),:,:)),'all'));
% halfe(:,:,1) = ALFE_fd(heq(:,:,1),'f_link', xover,'fs',fs,'L_target',ALFE_abs);
% halfe(:,:,2) = ALFE_fd(heq(:,:,2),'f_link', xover,'fs',fs,'L_target',ALFE_abs);
% 
% if doplots
%     Halfe = ffth(halfe);
%     quickplotHRTF(Halfe,fs)
%     sgtitle('HRTF after EQ+ALFE')
%     quickplotHRIR(halfe,fs)
%     sgtitle('HRIRs after EQ+ALFE')
% end

%% Remove ITDs

if saveITD

    pad = round(0.00075*fs); % 0.75ms padding (empirically set, changed from 1ms by Rapolas to fix negate delays)
%     [~,onset_seconds] = itdestimator(permute(h,[2,3,1]),'MaxIACCe','fs',fs);
    [~,delay_seconds] = itdestimator(permute(h,[2,3,1]),'fs',fs,'threshlvl',-10);

    hAlign = zeros(size(h));
    hwinAlign = zeros(size(hwin));
    if saveEQ
        heqAlign = zeros(size(heq));
    end
    if saveEQmp
        heqmpAlign = zeros(size(heqmp));
    end
    
    delay = round(delay_seconds*fs);
    delay = delay - pad;
    for i=1:ndirs
        for ch=1:2
            hAlign(:,i,ch) = circshift(h(:,i,ch),-delay(i,ch),1);
            hAlign(end-delay(i,ch)+1:end,i,ch) = 0;
            hwinAlign(:,i,ch) = circshift(hwin(:,i,ch),-delay(i,ch),1);
            hwinAlign(end-delay(i,ch)+1:end,i,ch) = 0;
            if saveEQ
                heqAlign(:,i,ch) = circshift(heq(:,i,ch),-delay(i,ch),1);
                heqAlign(end-delay(i,ch)+1:end,i,ch) = 0;
            end
            if saveEQmp
                heqmpAlign(:,i,ch) = circshift(heqmp(:,i,ch),-delay(i,ch),1);
                heqmpAlign(end-delay(i,ch)+1:end,i,ch) = 0;
            end
        end
    end

end
    
%% Save SOFA files

meta.pos = pos;
stimPar.Version = '3.0.1'; % copied from AA_hM
if saveITD
    scriptPath = which('AA_GenerateSOFA');
    exePath = ['"',scriptPath(1:end-17),'HRTF_SOFATo3DTI.exe"'];
end

for i=1:numel(targetFs)

    tFs = targetFs(i);
    stimPar.SamplingRate = tFs;
    if saveITD
        delay_re = round(delay*tFs/fs);
    end
    
    tFsdir = sprintf('%s/HRTF/%0.2dkHz',workdir,round(tFs/1000));
    if ~isfolder(tFsdir)
        mkdir(tFsdir)
    end
    
    % Raw HRIRs
    if saveRaw
        if tFs ~= fs
            h_re = resample(h,tFs,fs);
        else
            h_re = h;
        end
        % Save SOFA
        Obj.Data.IR=shiftdim(h_re,1);
        meta.delay = [];
        newobj = AA_SOFAsaveSimpleFreeFieldHRIRImperialCollege(sprintf('%s/%s_Raw_%0.2dkHz.sofa',tFsdir,sofaname,round(tFs/1000)),Obj,meta,stimPar);
        if doplots && tFs == max(targetFs) % only plot for the highest sampling frequency
            figure('Visible','off','pos',[10.6000 63.4000 695.2000 284.8000])
            subplot(1,2,1), SOFAplotHRTF(newobj,'MagHorizontal', 1); xlim([0 20000]), ylim([-180 180]), title('Left')
            subplot(1,2,2), SOFAplotHRTF(newobj,'MagHorizontal', 2); xlim([0 20000]), ylim([-180 180]), title('Right')
            sgtitle(sprintf('Magnitude Horizontal plane: %s Raw %0.2dkHz',sofaname,round(tFs/1000)));
            saveas(gcf,sprintf('%s/%s_Raw_%0.2dkHz_MagHorPlane.png',figdir,sofaname,round(tFs/1000)))
            close(gcf)
            figure('Visible','off','pos',[7.4000 48.2000 808.8000 606.4000])
            quickplotHRTF(h_re,tFs)
            sgtitle(sprintf('%s_Raw_%0.2dkHz, all %d HRIRs',sofaname,round(tFs/1000),size(h_re,2)),'interpreter','none')
            saveas(gcf,sprintf('%s/%s_Raw_%0.2dkHz_AllHRIRs.png',figdir,sofaname,round(tFs/1000)))
            close(gcf)
            figure('Visible','off','pos',[42 79 560 420])
            quickplotITD(h_re,pos,tFs)
            title(sprintf('%s_Raw_%0.2dkHz, ITDs',sofaname,round(tFs/1000)),'interpreter','none')
            saveas(gcf,sprintf('%s/%s_Raw_%0.2dkHz_ITDs.png',figdir,sofaname,round(tFs/1000)))
            close(gcf)
        end
    end
    
    % Raw HRIRs, no ITDs
    if saveRaw && saveITD
        if tFs ~= fs
            h_re = resample(hAlign,tFs,fs);
        else
            h_re = hAlign;
        end
        % Save SOFA
        Obj.Data.IR=shiftdim(h_re,1);
        meta.delay = delay_re;
        newobj = AA_SOFAsaveSimpleFreeFieldHRIRImperialCollege(sprintf('%s/%s_Raw_NoITD_%0.2dkHz.sofa',tFsdir,sofaname,round(tFs/1000)),Obj,meta,stimPar);
        % Save 3DTI
        system( sprintf('%s -i "%s/%s_Raw_NoITD_%0.2dkHz.sofa" -o "%s/%s_Raw_NoITD_%0.2dkHz.3dti-hrtf"',exePath,tFsdir,sofaname,round(tFs/1000),tFsdir,sofaname,round(tFs/1000)) );
        if doplots && tFs == max(targetFs) % only plot for the highest sampling frequency
            figure('Visible','off','pos',[10.6000 63.4000 695.2000 284.8000])
            subplot(1,2,1), SOFAplotHRTF(newobj,'MagHorizontal', 1); xlim([0 20000]), ylim([-180 180]), title('Left')
            subplot(1,2,2), SOFAplotHRTF(newobj,'MagHorizontal', 2); xlim([0 20000]), ylim([-180 180]), title('Right')
            sgtitle(sprintf('Magnitude Horizontal plane: %s Raw No ITD %0.2dkHz',sofaname,round(tFs/1000)));
            saveas(gcf,sprintf('%s/%s_Raw_NoITD_%0.2dkHz_MagHorPlane.png',figdir,sofaname,round(tFs/1000)))
            close(gcf)
            figure('Visible','off','pos',[7.4000 48.2000 808.8000 606.4000])
            quickplotHRTF(h_re,tFs)
            sgtitle(sprintf('%s_Raw_NoITD_%0.2dkHz, all %d HRIRs',sofaname,round(tFs/1000),size(h_re,2)),'interpreter','none')
            saveas(gcf,sprintf('%s/%s_Raw_NoITD_%0.2dkHz_AllHRIRs.png',figdir,sofaname,round(tFs/1000)))
            close(gcf)
            figure('Visible','off','pos',[42 79 560 420])
            quickplotITD(h_re,pos,tFs)
            title(sprintf('%s_Raw_NoITD_%0.2dkHz, ITDs',sofaname,round(tFs/1000)),'interpreter','none')
            saveas(gcf,sprintf('%s/%s_Raw_NoITD_%0.2dkHz_ITDs.png',figdir,sofaname,round(tFs/1000)))
            close(gcf)
        end
    end
    
    % Windowed HRIRs
    if saveWin
        if tFs ~= fs
            h_re = resample(hwin,tFs,fs);
        else
            h_re = hwin;
        end
        % Zero-pad to next multiple of two
        hlen = 2^nextpow2(size(h_re,1));
        h_re = [h_re;zeros(hlen-size(h_re,1),size(h_re,2),size(h_re,3))];
        % Save SOFA
        Obj.Data.IR=shiftdim(h_re,1);
        meta.delay = [];
        newobj = AA_SOFAsaveSimpleFreeFieldHRIRImperialCollege(sprintf('%s/%s_Windowed_%0.2dkHz.sofa',tFsdir,sofaname,round(tFs/1000)),Obj,meta,stimPar);
        if doplots && tFs == max(targetFs) % only plot for the highest sampling frequency
            figure('Visible','off','pos',[10.6000 63.4000 695.2000 284.8000])
            subplot(1,2,1), SOFAplotHRTF(newobj,'MagHorizontal', 1); xlim([0 20000]), ylim([-180 180]), title('Left')
            subplot(1,2,2), SOFAplotHRTF(newobj,'MagHorizontal', 2); xlim([0 20000]), ylim([-180 180]), title('Right')
            sgtitle(sprintf('Magnitude Horizontal plane: %s Windowed %0.2dkHz',sofaname,round(tFs/1000)));
            saveas(gcf,sprintf('%s/%s_Windowed_%0.2dkHz_MagHorPlane.png',figdir,sofaname,round(tFs/1000)))
            close(gcf)
            figure('Visible','off','pos',[7.4000 48.2000 808.8000 606.4000])
            quickplotHRTF(h_re,tFs)
            sgtitle(sprintf('%s_Windowed_%0.2dkHz, all %d HRIRs',sofaname,round(tFs/1000),size(h_re,2)),'interpreter','none')
            saveas(gcf,sprintf('%s/%s_Windowed_%0.2dkHz_AllHRIRs.png',figdir,sofaname,round(tFs/1000)))
            close(gcf)
            figure('Visible','off','pos',[42 79 560 420])
            quickplotITD(h_re,pos,tFs)
            title(sprintf('%s_Windowed_%0.2dkHz, ITDs',sofaname,round(tFs/1000)),'interpreter','none')
            saveas(gcf,sprintf('%s/%s_Windowed_%0.2dkHz_ITDs.png',figdir,sofaname,round(tFs/1000)))
            close(gcf)
        end
    end
    
    % Windowed HRIRs, no ITDs
    if saveWin && saveITD
        if tFs ~= fs
            h_re = resample(hwinAlign,tFs,fs);
        else
            h_re = hwinAlign;
        end
        % Zero-pad to next multiple of two
        hlen = 2^nextpow2(size(h_re,1));
        h_re = [h_re;zeros(hlen-size(h_re,1),size(h_re,2),size(h_re,3))];
        % Save SOFA
        Obj.Data.IR=shiftdim(h_re,1);
        meta.delay = delay_re;
        newobj = AA_SOFAsaveSimpleFreeFieldHRIRImperialCollege(sprintf('%s/%s_Windowed_NoITD_%0.2dkHz.sofa',tFsdir,sofaname,round(tFs/1000)),Obj,meta,stimPar);
        % Save 3DTI
        system( sprintf('%s -i "%s/%s_Windowed_NoITD_%0.2dkHz.sofa" -o "%s/%s_Windowed_NoITD_%0.2dkHz.3dti-hrtf"',exePath,tFsdir,sofaname,round(tFs/1000),tFsdir,sofaname,round(tFs/1000)) );
        if doplots && tFs == max(targetFs) % only plot for the highest sampling frequency
            figure('Visible','off','pos',[10.6000 63.4000 695.2000 284.8000])
            subplot(1,2,1), SOFAplotHRTF(newobj,'MagHorizontal', 1); xlim([0 20000]), ylim([-180 180]), title('Left')
            subplot(1,2,2), SOFAplotHRTF(newobj,'MagHorizontal', 2); xlim([0 20000]), ylim([-180 180]), title('Right')
            sgtitle(sprintf('Magnitude Horizontal plane: %s Windowed No ITD %0.2dkHz',sofaname,round(tFs/1000)));
            saveas(gcf,sprintf('%s/%s_Windowed_NoITD_%0.2dkHz_MagHorPlane.png',figdir,sofaname,round(tFs/1000)))
            close(gcf)
            figure('Visible','off','pos',[7.4000 48.2000 808.8000 606.4000])
            quickplotHRTF(h_re,tFs)
            sgtitle(sprintf('%s_Windowed_NoITD_%0.2dkHz, all %d HRIRs',sofaname,round(tFs/1000),size(h_re,2)),'interpreter','none')
            saveas(gcf,sprintf('%s/%s_Windowed_NoITD_%0.2dkHz_AllHRIRs.png',figdir,sofaname,round(tFs/1000)))
            close(gcf)
            figure('Visible','off','pos',[42 79 560 420])
            quickplotITD(h_re,pos,tFs)
            title(sprintf('%s_Windowed_NoITD_%0.2dkHz, ITDs',sofaname,round(tFs/1000)),'interpreter','none')
            saveas(gcf,sprintf('%s/%s_Windowed_NoITD_%0.2dkHz_ITDs.png',figdir,sofaname,round(tFs/1000)))
            close(gcf)
        end
    end

    % EQ (free-field compensated)
    if saveEQ
        if tFs ~= fs
            h_re = resample(heq,tFs,fs);
        else
            h_re = heq;
        end
        % Zero-pad to next multiple of two
        hlen = 2^nextpow2(size(h_re,1));
        h_re = [h_re;zeros(hlen-size(h_re,1),size(h_re,2),size(h_re,3))];
        % Save SOFA
        Obj.Data.IR=shiftdim(h_re,1);
        meta.delay = [];
        newobj = AA_SOFAsaveSimpleFreeFieldHRIRImperialCollege(sprintf('%s/%s_FreeFieldComp_%0.2dkHz.sofa',tFsdir,sofaname,round(tFs/1000)),Obj,meta,stimPar);
        if doplots && tFs == max(targetFs) % only plot for the highest sampling frequency
            figure('Visible','off','pos',[10.6000 63.4000 695.2000 284.8000])
            subplot(1,2,1), SOFAplotHRTF(newobj,'MagHorizontal', 1); xlim([0 20000]), ylim([-180 180]), title('Left')
            subplot(1,2,2), SOFAplotHRTF(newobj,'MagHorizontal', 2); xlim([0 20000]), ylim([-180 180]), title('Right')
            sgtitle(sprintf('Magnitude Horizontal plane: %s FreeFieldComp %0.2dkHz',sofaname,round(tFs/1000)));
            saveas(gcf,sprintf('%s/%s_FreeFieldComp_%0.2dkHz_MagHorPlane.png',figdir,sofaname,round(tFs/1000)))
            close(gcf)
            figure('Visible','off','pos',[7.4000 48.2000 808.8000 606.4000])
            quickplotHRTF(h_re,tFs)
            sgtitle(sprintf('%s_FreeFieldComp_%0.2dkHz, all %d HRIRs',sofaname,round(tFs/1000),size(h_re,2)),'interpreter','none')
            saveas(gcf,sprintf('%s/%s_FreeFieldComp_%0.2dkHz_AllHRIRs.png',figdir,sofaname,round(tFs/1000)))
            close(gcf)
            figure('Visible','off','pos',[42 79 560 420])
            quickplotITD(h_re,pos,tFs)
            title(sprintf('%s_FreeFieldComp_%0.2dkHz, ITDs',sofaname,round(tFs/1000)),'interpreter','none')
            saveas(gcf,sprintf('%s/%s_FreeFieldComp_%0.2dkHz_ITDs.png',figdir,sofaname,round(tFs/1000)))
            close(gcf)
        end
    end
    
    % EQ no ITDs
    if saveEQ && saveITD
        if tFs ~= fs
            h_re = resample(heqAlign,tFs,fs);
        else
            h_re = heqAlign;
        end
        % Zero-pad to next multiple of two
        hlen = 2^nextpow2(size(h_re,1));
        h_re = [h_re;zeros(hlen-size(h_re,1),size(h_re,2),size(h_re,3))];
        % Save SOFA
        Obj.Data.IR=shiftdim(h_re,1);
        meta.delay = delay_re;
        newobj = AA_SOFAsaveSimpleFreeFieldHRIRImperialCollege(sprintf('%s/%s_FreeFieldComp_NoITD_%0.2dkHz.sofa',tFsdir,sofaname,round(tFs/1000)),Obj,meta,stimPar);
        % Save 3DTI
        system( sprintf('%s -i "%s/%s_FreeFieldComp_NoITD_%0.2dkHz.sofa" -o "%s/%s_FreeFieldComp_NoITD_%0.2dkHz.3dti-hrtf"',exePath,tFsdir,sofaname,round(tFs/1000),tFsdir,sofaname,round(tFs/1000)) );
        if doplots && tFs == max(targetFs) % only plot for the highest sampling frequency
            figure('Visible','off','pos',[10.6000 63.4000 695.2000 284.8000])
            subplot(1,2,1), SOFAplotHRTF(newobj,'MagHorizontal', 1); xlim([0 20000]), ylim([-180 180]), title('Left')
            subplot(1,2,2), SOFAplotHRTF(newobj,'MagHorizontal', 2); xlim([0 20000]), ylim([-180 180]), title('Right')
            sgtitle(sprintf('Magnitude Horizontal plane: %s FreeFieldComp NoITD %0.2dkHz',sofaname,round(tFs/1000)));
            saveas(gcf,sprintf('%s/%s_FreeFieldComp_NoITD_%0.2dkHz_MagHorPlane.png',figdir,sofaname,round(tFs/1000)))
            close(gcf)
            figure('Visible','off','pos',[7.4000 48.2000 808.8000 606.4000])
            quickplotHRTF(h_re,tFs)
            sgtitle(sprintf('%s_FreeFieldComp_NoITD_%0.2dkHz, all %d HRIRs',sofaname,round(tFs/1000),size(h_re,2)),'interpreter','none')
            saveas(gcf,sprintf('%s/%s_FreeFieldComp_NoITD_%0.2dkHz_AllHRIRs.png',figdir,sofaname,round(tFs/1000)))
            close(gcf)
            figure('Visible','off','pos',[42 79 560 420])
            quickplotITD(h_re,pos,tFs)
            title(sprintf('%s_FreeFieldComp_NoITD_%0.2dkHz, ITDs',sofaname,round(tFs/1000)),'interpreter','none')
            saveas(gcf,sprintf('%s/%s_FreeFieldComp_NoITD_%0.2dkHz_ITDs.png',figdir,sofaname,round(tFs/1000)))
            close(gcf)
        end
    end
    
    % EQ minimum phase
    if saveEQmp
        if tFs ~= fs
            h_re = resample(heqmp,tFs,fs);
        else
            h_re = heqmp;
        end
        % Zero-pad to next multiple of two
        hlen = 2^nextpow2(size(h_re,1));
        h_re = [h_re;zeros(hlen-size(h_re,1),size(h_re,2),size(h_re,3))];
        % Save SOFA
        Obj.Data.IR=shiftdim(h_re,1);
        meta.delay = [];
        newobj = AA_SOFAsaveSimpleFreeFieldHRIRImperialCollege(sprintf('%s/%s_FreeFieldCompMinPhase_%0.2dkHz.sofa',tFsdir,sofaname,round(tFs/1000)),Obj,meta,stimPar);
        if doplots && tFs == max(targetFs) % only plot for the highest sampling frequency
            figure('Visible','off','pos',[10.6000 63.4000 695.2000 284.8000])
            subplot(1,2,1), SOFAplotHRTF(newobj,'MagHorizontal', 1); xlim([0 20000]), ylim([-180 180]), title('Left')
            subplot(1,2,2), SOFAplotHRTF(newobj,'MagHorizontal', 2); xlim([0 20000]), ylim([-180 180]), title('Right')
            sgtitle(sprintf('Magnitude Horizontal plane: %s FreeFieldCompMinPhase %0.2dkHz',sofaname,round(tFs/1000)));
            saveas(gcf,sprintf('%s/%s_FreeFieldCompMinPhase_%0.2dkHz_MagHorPlane.png',figdir,sofaname,round(tFs/1000)))
            close(gcf)
            figure('Visible','off','pos',[7.4000 48.2000 808.8000 606.4000])
            quickplotHRTF(h_re,tFs)
            sgtitle(sprintf('%s_FreeFieldCompMinPhase_%0.2dkHz, all %d HRIRs',sofaname,round(tFs/1000),size(h_re,2)),'interpreter','none')
            saveas(gcf,sprintf('%s/%s_FreeFieldCompMinPhase_%0.2dkHz_AllHRIRs.png',figdir,sofaname,round(tFs/1000)))
            close(gcf)
            figure('Visible','off','pos',[42 79 560 420])
            quickplotITD(h_re,pos,tFs)
            title(sprintf('%s_FreeFieldCompMinPhase_%0.2dkHz, ITDs',sofaname,round(tFs/1000)),'interpreter','none')
            saveas(gcf,sprintf('%s/%s_FreeFieldCompMinPhase_%0.2dkHz_ITDs.png',figdir,sofaname,round(tFs/1000)))
            close(gcf)
        end
    end
    
    % EQ min phase no ITDs
    if saveEQmp && saveITD
        if tFs ~= fs
            h_re = resample(heqmpAlign,tFs,fs);
        else
            h_re = heqmpAlign;
        end
        % Zero-pad to next multiple of two
        hlen = 2^nextpow2(size(h_re,1));
        h_re = [h_re;zeros(hlen-size(h_re,1),size(h_re,2),size(h_re,3))];
        % Save SOFA
        Obj.Data.IR=shiftdim(h_re,1);
        meta.delay = delay_re;
        newobj = AA_SOFAsaveSimpleFreeFieldHRIRImperialCollege(sprintf('%s/%s_FreeFieldCompMinPhase_NoITD_%0.2dkHz.sofa',tFsdir,sofaname,round(tFs/1000)),Obj,meta,stimPar);
        % Save 3DTI
        system( sprintf('%s -i "%s/%s_FreeFieldCompMinPhase_NoITD_%0.2dkHz.sofa" -o "%s/%s_FreeFieldCompMinPhase_NoITD_%0.2dkHz.3dti-hrtf"',exePath,tFsdir,sofaname,round(tFs/1000),tFsdir,sofaname,round(tFs/1000)) );
        if doplots && tFs == max(targetFs) % only plot for the highest sampling frequency
            figure('Visible','off','pos',[10.6000 63.4000 695.2000 284.8000])
            subplot(1,2,1), SOFAplotHRTF(newobj,'MagHorizontal', 1); xlim([0 20000]), ylim([-180 180]), title('Left')
            subplot(1,2,2), SOFAplotHRTF(newobj,'MagHorizontal', 2); xlim([0 20000]), ylim([-180 180]), title('Right')
            sgtitle(sprintf('Magnitude Horizontal plane: %s FreeFieldCompMinPhase NoITD %0.2dkHz',sofaname,round(tFs/1000)));
            saveas(gcf,sprintf('%s/%s_FreeFieldCompMinPhase_NoITD_%0.2dkHz_MagHorPlane.png',figdir,sofaname,round(tFs/1000)))
            close(gcf)
            figure('Visible','off','pos',[7.4000 48.2000 808.8000 606.4000])
            quickplotHRTF(h_re,tFs)
            sgtitle(sprintf('%s_FreeFieldCompMinPhase_NoITD_%0.2dkHz, all %d HRIRs',sofaname,round(tFs/1000),size(h_re,2)),'interpreter','none')
            saveas(gcf,sprintf('%s/%s_FreeFieldCompMinPhase_NoITD_%0.2dkHz_AllHRIRs.png',figdir,sofaname,round(tFs/1000)))
            close(gcf)
            figure('Visible','off','pos',[42 79 560 420])
            quickplotITD(h_re,pos,tFs)
            title(sprintf('%s_FreeFieldCompMinPhase_NoITD_%0.2dkHz, ITDs',sofaname,round(tFs/1000)),'interpreter','none')
            saveas(gcf,sprintf('%s/%s_FreeFieldCompMinPhase_NoITD_%0.2dkHz_ITDs.png',figdir,sofaname,round(tFs/1000)))
            close(gcf)
        end
    end

end

end

%% Aux functions

function quickplotHRTF(h,fs)
    H = ffth(h);
    nfreqs = size(H,1);
    irLen = size(h,1);
    f = linspace(0,fs/2,nfreqs).';
    t = 1000*(0:(irLen-1))/fs; % in ms
    havg = mean(abs(h),2);
    Hmag_avg = mean(abs(H),2); % avg across directions
    subplot(2,2,1), plot([nan nan],[nan nan],'k','LineWidth',2), hold on
    plot(t,db(abs(h(:,:,1))),'Color',[0.4 0.4 0.4],'LineWidth',0.5), title('Left HRIRs (time)')
    plot(t,db(havg(:,:,1)),'k','LineWidth',2)
    legend('Mean')
    grid on, xlim([0 irLen/fs*1000]), ylim([-70, -10])
    xlabel('Time (ms)'), ylabel('Amplitude (dB)')
    subplot(2,2,2), plot(t,db(abs(h(:,:,2))),'Color',[0.4 0.4 0.4],'LineWidth',0.5), hold on
    plot(t,db(havg(:,:,2)),'k','LineWidth',2), title('Right HRIRs (time)')
    grid on, xlim([0 irLen/fs*1000]), ylim([-70, -10])
    xlabel('Time (ms)'), ylabel('Amplitude (dB)')
    subplot(2,2,3), semilogx(f,db(abs(H(:,:,1))),'Color',[0.4 0.4 0.4],'LineWidth',0.5), hold on
    semilogx(f,db(Hmag_avg(:,:,1)),'k','LineWidth',2), title('Left HRIRs (mag. spectra)')
    grid on, xlim([f(2) fs/2]), ylim([-45, 15])
    xlabel('Frequency (Hz)'), ylabel('Amplitude (dB)')
    subplot(2,2,4), semilogx(f,db(abs(H(:,:,2))),'Color',[0.4 0.4 0.4],'LineWidth',0.5), hold on
    semilogx(f,db(Hmag_avg(:,:,2)),'k','LineWidth',2),title('Right HRIRs (mag. spectra)')
    grid on, xlim([f(2) fs/2]), ylim([-45, 15])
    xlabel('Frequency (Hz)'), ylabel('Amplitude (dB)')
end

function quickplotITD(h,pos,fs)
    itd = itdestimator(permute(h,[2,3,1]),'fs',fs,'threshlvl',-10);
    az = pos(:,1);
    el = pos(:,2);
    unique_el = sort(unique(el));
    leg = {};
    colors = parula(numel(unique_el)+1);
    for i=1:numel(unique_el)
        curr_el = unique_el(i);
        ind = find(abs(el-curr_el)<0.1);
        [~,order] = sort(az(ind));
        ind = ind(order);
        curr_az = az(ind);
        curr_itd = itd(ind);
        plot(curr_az,curr_itd,'color',colors(i,:)), hold on
        leg{i} = strcat('El=',num2str(curr_el),'°');
    end
    grid on, ylim([-8e-4 8e-4])
    legend(leg,'fontsize',6,'location','se')
    title('ITD')
    xlabel('Azimuth (°)'), ylabel('t (s)')
end