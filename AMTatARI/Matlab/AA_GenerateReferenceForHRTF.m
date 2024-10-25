
clear

% NOTE: ignore the warnings thrown by AA_GenerateSOFA
amt_start
% Generate reference
dirnameL = 'Z:\Private\2022_SONICOM-HRTF-DATASET\Reference measurements\20240815_ReferenceMeasurementL';
dirnameR = 'Z:\Private\2022_SONICOM-HRTF-DATASET\Reference measurements\20240815_ReferenceMeasurementR';
sofaname = 'Reference';
doplots = 1;
targetFs = 96000;
elSp = [-45,-30:10:30,45:15:150,160:10:210,225]; % speaker elevations

disp('Please ignore the next two warnings:')
AA_GenerateSOFA([sofaname,'_left'],dirnameL,[dirnameL,'\settings.AMTatARI'],[dirnameL,'\itemlist.itl.csv'],'',0,0,1,0,0,0,targetFs);
AA_GenerateSOFA([sofaname,'_right'],dirnameR,[dirnameR,'\settings.AMTatARI'],[dirnameR,'\itemlist.itl.csv'],'',0,0,1,0,0,0,targetFs);

if doplots
    AA_QuickPlotIR([sofaname,'_left'],dirnameL,[dirnameL,'\settings.AMTatARI'],[dirnameL,'\itemlist.itl.csv'],'indAz',1)
    AA_QuickPlotIR([sofaname,'_right'],dirnameR,[dirnameR,'\settings.AMTatARI'],[dirnameR,'\itemlist.itl.csv'],'indAz',1)
end

% Load data
SOFApathL = sprintf('%s/HRTF/%0.2dkHz/%s_Windowed_%0.2dkHz.sofa',dirnameL,round(targetFs/1000),[sofaname,'_left'],round(targetFs/1000));
SOFApathR = sprintf('%s/HRTF/%0.2dkHz/%s_Windowed_%0.2dkHz.sofa',dirnameR,round(targetFs/1000),[sofaname,'_right'],round(targetFs/1000));
SOFAobjL = SOFAload(SOFApathL);
SOFAobjR = SOFAload(SOFApathR);
[hL,fs,az,el] = sofa2hrtf(SOFAobjL);
hR = sofa2hrtf(SOFAobjR);
h = cat(3,hL(:,:,1),hR(:,:,2));
nEl = numel(el);
clear SOFAobjL SOFAobjR

figure
subplot(3,2,1), AKp(h(:,:,1),'et2d','fs',fs)
subplot(3,2,2), AKp(h(:,:,2),'et2d','fs',fs)
subplot(3,2,3), AKp(h(:,:,1),'m2d','fs',fs)
subplot(3,2,4), AKp(h(:,:,2),'m2d','fs',fs)
subplot(3,2,5), AKp(h(:,:,1),'pu2d','fs',fs)
subplot(3,2,6), AKp(h(:,:,2),'pu2d','fs',fs)
sgtitle('Original measurement')

%% Check left/right delays (will affect ITDs so it is important)
% NOTE: they are fine. At first they looked bad because I was looking at
% the right channel of the left-mic-only measurument
itd = itdestimator(permute(h,[2,3,1]),'MaxIACCe','fs',fs);
figure, plot(itd*1e6), ylabel('\Deltat (\mus)'), xlabel('Speaker #'), grid on
title('Onset time difference between reference L/R (should be below ITD JND)')

%% Some preprocessing

% Zero-pad
hlen = 2^16;
hpad = [h;zeros(hlen-size(h,1),size(h,2),size(h,3))];

% To freq. domain
H = ffth(hpad);
nfreqs = size(H,1);
f = linspace(0,fs/2,nfreqs);

figure
subplot(3,2,1), AKp(hpad(:,:,1),'et2d','fs',fs)
subplot(3,2,2), AKp(hpad(:,:,2),'et2d','fs',fs)
subplot(3,2,3), AKp(hpad(:,:,1),'m2d','fs',fs)
subplot(3,2,4), AKp(hpad(:,:,2),'m2d','fs',fs)
subplot(3,2,5), AKp(hpad(:,:,1),'pu2d','fs',fs)
subplot(3,2,6), AKp(hpad(:,:,2),'pu2d','fs',fs)
sgtitle('Zero-padded')

% Smooth, keeping phase untouched
Hsm(:,:,1) = AKfractOctSmooth(H(:,:,1),'eqv',fs,6);
Hsm(:,:,2) = AKfractOctSmooth(H(:,:,2),'eqv',fs,6);
hsmpad = iffth(Hsm);

figure
subplot(3,2,1), AKp(hsmpad(:,:,1),'et2d','fs',fs)
subplot(3,2,2), AKp(hsmpad(:,:,2),'et2d','fs',fs)
subplot(3,2,3), AKp(hsmpad(:,:,1),'m2d','fs',fs)
subplot(3,2,4), AKp(hsmpad(:,:,2),'m2d','fs',fs)
subplot(3,2,5), AKp(hsmpad(:,:,1),'pu2d','fs',fs)
subplot(3,2,6), AKp(hsmpad(:,:,2),'pu2d','fs',fs)
sgtitle('Magnitude smoothed')

%H = Hsm; % comment out to use smooth response for filter

%% Inverse filter

% get average amplitude for the inverse filter
flims = [100 15000];
flims_samples = round(flims*2*nfreqs/fs)+1;
avgAmp = mean(abs(H(flims_samples(1):flims_samples(2),:,:)),'all');

% inverse filter
maxAmp = 20;
frac = 0;
T = avgAmp*ones(nfreqs,1); % target is a flat response
for ch=1:2
    for i=1:nEl
        [EQmp(:,i,ch),EQ(:,i,ch)] = autoreg_minphase(H(:,i,ch),T,fs,maxAmp,frac,flims);
        eq(:,i,ch) = iffth(EQ(:,i,ch));
        eqmp(:,i,ch) = iffth(EQmp(:,i,ch));
    end
end

figure
subplot(3,2,1), AKp(eq(:,:,1),'et2d','fs',fs)
subplot(3,2,2), AKp(eq(:,:,2),'et2d','fs',fs)
subplot(3,2,3), AKp(eq(:,:,1),'m2d','fs',fs)
subplot(3,2,4), AKp(eq(:,:,2),'m2d','fs',fs)
subplot(3,2,5), AKp(eq(:,:,1),'pu2d','fs',fs)
subplot(3,2,6), AKp(eq(:,:,2),'pu2d','fs',fs)
sgtitle('EQ filter')

% center (TODO: Kaiser-windowed sinc function?)
delay = size(eq,1)/2;
safety = 64;
eqwin = circshift(eq,delay);

% window
winhann = hann(size(eq,1));
eqwin = winhann.*eqwin;

figure
subplot(3,2,1), AKp(eqwin(:,:,1),'et2d','fs',fs)
subplot(3,2,2), AKp(eqwin(:,:,2),'et2d','fs',fs)
subplot(3,2,3), AKp(eqwin(:,:,1),'m2d','fs',fs)
subplot(3,2,4), AKp(eqwin(:,:,2),'m2d','fs',fs)
subplot(3,2,5), AKp(eqwin(:,:,1),'pu2d','fs',fs)
subplot(3,2,6), AKp(eqwin(:,:,2),'pu2d','fs',fs)
sgtitle('EQ filter, shifted and windowed')

figure
subplot(3,2,1), AKp(eqmp(:,:,1),'et2d','fs',fs)
subplot(3,2,2), AKp(eqmp(:,:,2),'et2d','fs',fs)
subplot(3,2,3), AKp(eqmp(:,:,1),'m2d','fs',fs)
subplot(3,2,4), AKp(eqmp(:,:,2),'m2d','fs',fs)
subplot(3,2,5), AKp(eqmp(:,:,1),'pu2d','fs',fs)
subplot(3,2,6), AKp(eqmp(:,:,2),'pu2d','fs',fs)
sgtitle('EQ filter min phase')

% Alternative shorter EQ filter
% eqshort = circshift(eq,safety);
% fadelenin = 16;
% fadelenout = 128;
% tin = linspace(0,pi/2,fadelenin).';
% tout = linspace(0,pi/2,fadelenout).';
% fadein = sin(tin).^2;
% fadeout = cos(tout).^2;
% irLen = size(h,1);
% win = [fadein; ones(irLen-fadelenin-fadelenout,1); fadeout];
% eqshort = win.*eqshort(1:irLen,:,:);
% 
% figure
% subplot(3,2,1), AKp(eqshort(:,:,1),'et2d','fs',fs)
% subplot(3,2,2), AKp(eqshort(:,:,2),'et2d','fs',fs)
% subplot(3,2,3), AKp(eqshort(:,:,1),'m2d','fs',fs)
% subplot(3,2,4), AKp(eqshort(:,:,2),'m2d','fs',fs)
% subplot(3,2,5), AKp(eqshort(:,:,1),'pu2d','fs',fs)
% subplot(3,2,6), AKp(eqshort(:,:,2),'pu2d','fs',fs)
% sgtitle('EQ filter, short')

eq = eqwin;

%% Plot expected results after EQ

for ch=1:2
    for i=1:nEl
        convlen = size(h,1) + size(eq,1) - 1;
        nfft = 2^nextpow2(convlen);
        heq = iffth( ffth(h,nfft) .* ffth(eq,nfft) );
        heq = heq(1:convlen,:,:);
        heqmp = iffth( ffth(h,nfft) .* ffth(eqmp,nfft) );
        heqmp = heqmp(1:convlen,:,:);
        % time-shift back
        heq = circshift(heq,-delay+safety);
    end
end

figure
subplot(3,2,1), AKp(heq(:,:,1),'et2d','fs',fs)
subplot(3,2,2), AKp(heq(:,:,2),'et2d','fs',fs)
subplot(3,2,3), AKp(heq(:,:,1),'m2d','fs',fs)
subplot(3,2,4), AKp(heq(:,:,2),'m2d','fs',fs)
subplot(3,2,5), AKp(heq(:,:,1),'p2d','fs',fs)
subplot(3,2,6), AKp(heq(:,:,2),'p2d','fs',fs)
sgtitle('Equalised measurement')

figure
subplot(3,2,1), AKp(heqmp(:,:,1),'et2d','fs',fs)
subplot(3,2,2), AKp(heqmp(:,:,2),'et2d','fs',fs)
subplot(3,2,3), AKp(heqmp(:,:,1),'m2d','fs',fs)
subplot(3,2,4), AKp(heqmp(:,:,2),'m2d','fs',fs)
subplot(3,2,5), AKp(heqmp(:,:,1),'p2d','fs',fs)
subplot(3,2,6), AKp(heqmp(:,:,2),'p2d','fs',fs)
sgtitle('Equalised measurement, min phase')

% Window
fadelenin = 16;
fadelenout = 128;
tin = linspace(0,pi/2,fadelenin).';
tout = linspace(0,pi/2,fadelenout).';
fadein = sin(tin).^2;
fadeout = cos(tout).^2;
irLen = size(h,1);
win = [fadein; ones(irLen-fadelenin-fadelenout,1); fadeout];
heqwin = win.*heq(1:irLen,:,:);
heqmpwin = win.*heqmp(1:irLen,:,:);

% Check energy loss
nrg = sum(abs(heq).^2,'all');
nrgwin = sum(abs(heqwin).^2,'all');
nrgloss = 1-nrgwin/nrg;
if nrgloss>0.01
    warning('Energy was lost after windowing')
end
        
figure
subplot(3,2,1), AKp(heqwin(:,:,1),'et2d','fs',fs)
subplot(3,2,2), AKp(heqwin(:,:,2),'et2d','fs',fs)
subplot(3,2,3), AKp(heqwin(:,:,1),'m2d','fs',fs)
subplot(3,2,4), AKp(heqwin(:,:,2),'m2d','fs',fs)
subplot(3,2,5), AKp(heqwin(:,:,1),'p2d','fs',fs)
subplot(3,2,6), AKp(heqwin(:,:,2),'p2d','fs',fs)
sgtitle('Equalised measurement, after windowing')

figure
subplot(3,2,1), AKp(heqmpwin(:,:,1),'et2d','fs',fs)
subplot(3,2,2), AKp(heqmpwin(:,:,2),'et2d','fs',fs)
subplot(3,2,3), AKp(heqmpwin(:,:,1),'m2d','fs',fs)
subplot(3,2,4), AKp(heqmpwin(:,:,2),'m2d','fs',fs)
subplot(3,2,5), AKp(heqmpwin(:,:,1),'p2d','fs',fs)
subplot(3,2,6), AKp(heqmpwin(:,:,2),'p2d','fs',fs)
sgtitle('Equalised measurement, min phase, after windowing')



%% Save
elrad = el;
el = elSp;
save('Z:\Private\2022_SONICOM-HRTF-DATASET\Reference measurements\reference_eq.mat','eq','eqmp','el','delay')
el = elrad;

%% Aux functions
function quickplotHRTF(h,fs)
    H = ffth(h);
    nfreqs = size(H,1);
    irLen = size(h,1);
    f = linspace(0,fs/2,nfreqs).';
    t = 1000*(0:(irLen-1))/fs; % in ms
    havg = mean(abs(h),2);
    Hmag_avg = mean(abs(H),2); % avg across directions
    figure('pos',[7.4000 48.2000 808.8000 606.4000])
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
    figure('pos',[42 79 560 420])
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