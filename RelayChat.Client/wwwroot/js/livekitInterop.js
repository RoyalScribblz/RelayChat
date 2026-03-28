let room = null;
let dotNetRef = null;
const attachedAudioElements = new Map();
const videoPublications = new Map();
const voiceParticipants = new Map();
const activeSpeakerIds = new Set();
const renderedVideoElements = [];
let screenShareTracks = [];
let focusedTileKey = null;
let secondaryTilesCollapsed = false;
let isDeafened = false;

function describeRoomState() {
    return {
        hasRoom: !!room,
        roomName: room?.name ?? null,
        connectionState: room?.state ?? null,
        localIdentity: room?.localParticipant?.identity ?? null,
        remoteParticipantCount: room?.remoteParticipants?.size ?? 0,
        voiceParticipantCount: voiceParticipants.size,
        videoPublicationCount: videoPublications.size,
        focusedTileKey
    };
}

function getAudioRoot() {
    let root = document.getElementById("livekit-audio-root");
    if (root) {
        return root;
    }

    root = document.createElement("div");
    root.id = "livekit-audio-root";
    root.style.display = "none";
    document.body.appendChild(root);
    return root;
}

function getAudioTrackKey(publication, participant) {
    return publication?.trackSid ?? publication?.sid ?? participant?.identity ?? crypto.randomUUID();
}

function getVideoRoot() {
    return document.getElementById("livekit-video-grid");
}

function getParticipantIdentity(participant) {
    return participant?.identity ?? room?.localParticipant?.identity ?? null;
}

function normalizeVideoSource(track, publication) {
    const source = publication?.source ?? track?.source ?? null;
    if (source === window.LivekitClient?.Track?.Source?.Camera || source === "camera") {
        return "camera";
    }

    if (source === window.LivekitClient?.Track?.Source?.ScreenShare || source === "screen_share") {
        return "screen";
    }

    return null;
}

function getVideoPublicationKey(identity, source) {
    return `${identity}|${source}`;
}

function getParticipantMeta(identity) {
    const participant = voiceParticipants.get(identity);
    if (participant) {
        return participant;
    }

    return {
        userId: identity,
        name: identity,
        handle: identity,
        avatarUrl: null,
        isMuted: false,
        isDeafened: false
    };
}

function getParticipantInitial(participant) {
    const value = participant?.name || participant?.handle || "?";
    return value.substring(0, 1).toUpperCase();
}

function isLocalIdentity(identity) {
    return !!identity && identity === room?.localParticipant?.identity;
}

function attachRemoteAudioTrack(track, publication, participant) {
    if (track.kind !== "audio") {
        return;
    }

    const key = getAudioTrackKey(publication, participant);
    if (attachedAudioElements.has(key)) {
        return;
    }

    const element = track.attach();
    element.autoplay = true;
    element.muted = isDeafened;
    element.playsInline = true;
    getAudioRoot().appendChild(element);
    attachedAudioElements.set(key, { element, track });
}

function detachRemoteAudioTrack(publication, participant) {
    const key = getAudioTrackKey(publication, participant);
    const existing = attachedAudioElements.get(key);
    if (!existing) {
        return;
    }

    existing.track.detach(existing.element);
    existing.element.remove();
    attachedAudioElements.delete(key);
}

function detachAllRemoteAudioTracks() {
    for (const { element, track } of attachedAudioElements.values()) {
        track.detach(element);
        element.remove();
    }

    attachedAudioElements.clear();
}

function applyPlaybackMute() {
    for (const { element } of attachedAudioElements.values()) {
        element.muted = isDeafened;
    }
}

function clearRenderedVideoElements() {
    for (const { element, track } of renderedVideoElements) {
        track.detach(element);
    }

    renderedVideoElements.length = 0;
}

function notifyActiveSpeakersChanged() {
    if (!dotNetRef) {
        return;
    }

    dotNetRef.invokeMethodAsync("HandleActiveSpeakersChanged", [...activeSpeakerIds]);
}

function notifyVideoParticipantsChanged() {
    if (!dotNetRef) {
        return;
    }

    const identities = [...new Set([...videoPublications.values()].map(entry => entry.identity))];
    dotNetRef.invokeMethodAsync("HandleVideoParticipantsChanged", identities);
}

function createBadge(text, styles = {}) {
    const badge = document.createElement("div");
    badge.textContent = text;
    badge.style.padding = "4px 8px";
    badge.style.borderRadius = "999px";
    badge.style.fontSize = "12px";
    badge.style.fontWeight = "600";
    badge.style.background = "rgba(7, 11, 16, 0.72)";
    badge.style.color = "#f5f7fa";
    for (const [key, value] of Object.entries(styles)) {
        badge.style[key] = value;
    }

    return badge;
}

function createSvgIcon(pathData, size = 14) {
    const ns = "http://www.w3.org/2000/svg";
    const svg = document.createElementNS(ns, "svg");
    svg.setAttribute("viewBox", "0 0 24 24");
    svg.setAttribute("width", String(size));
    svg.setAttribute("height", String(size));
    svg.style.display = "block";
    svg.style.flex = "0 0 auto";

    const path = document.createElementNS(ns, "path");
    path.setAttribute("d", pathData);
    path.setAttribute("fill", "currentColor");
    svg.appendChild(path);
    return svg;
}

function createIconBadge(icon, styles = {}) {
    const badge = document.createElement("div");
    badge.style.width = "30px";
    badge.style.height = "30px";
    badge.style.borderRadius = "999px";
    badge.style.display = "inline-flex";
    badge.style.alignItems = "center";
    badge.style.justifyContent = "center";
    badge.style.background = "rgba(7, 11, 16, 0.72)";
    badge.style.color = "#f5f7fa";
    for (const [key, value] of Object.entries(styles)) {
        badge.style[key] = value;
    }

    badge.appendChild(icon);
    return badge;
}

function createPlaceholderAvatar(participant) {
    if (participant.avatarUrl) {
        const image = document.createElement("img");
        image.src = participant.avatarUrl;
        image.alt = participant.name;
        image.style.width = "88px";
        image.style.height = "88px";
        image.style.borderRadius = "999px";
        image.style.objectFit = "cover";
        return image;
    }

    const avatar = document.createElement("div");
    avatar.textContent = getParticipantInitial(participant);
    avatar.style.width = "88px";
    avatar.style.height = "88px";
    avatar.style.borderRadius = "999px";
    avatar.style.display = "flex";
    avatar.style.alignItems = "center";
    avatar.style.justifyContent = "center";
    avatar.style.background = "#2a3442";
    avatar.style.color = "#f5f7fa";
    avatar.style.fontSize = "34px";
    avatar.style.fontWeight = "700";
    return avatar;
}

function buildVisualTiles() {
    const tiles = [];

    for (const participant of voiceParticipants.values()) {
        const identity = participant.userId;
        const cameraKey = getVideoPublicationKey(identity, "camera");
        const screenKey = getVideoPublicationKey(identity, "screen");
        const cameraPublication = videoPublications.get(cameraKey);
        const screenPublication = videoPublications.get(screenKey);

        tiles.push({
            key: cameraPublication ? cameraKey : `${identity}|placeholder`,
            identity,
            type: cameraPublication ? "camera" : "placeholder",
            label: participant.name,
            participant,
            videoPublication: cameraPublication ?? null
        });

        if (screenPublication) {
            tiles.push({
                key: screenKey,
                identity,
                type: "screen",
                label: `${participant.name} is sharing`,
                participant,
                videoPublication: screenPublication
            });
        }
    }

    for (const publication of videoPublications.values()) {
        if (voiceParticipants.has(publication.identity)) {
            continue;
        }

        tiles.push({
            key: publication.key,
            identity: publication.identity,
            type: publication.source === "screen" ? "screen" : "camera",
            label: publication.identity,
            participant: getParticipantMeta(publication.identity),
            videoPublication: publication
        });
    }

    return tiles;
}

function createTileElement(tile, isFocused, isSecondary) {
    const participant = tile.participant;
    const isSpeaking = activeSpeakerIds.has(tile.identity);

    const wrapper = document.createElement("button");
    wrapper.type = "button";
    wrapper.style.position = "relative";
    wrapper.style.display = "flex";
    wrapper.style.alignItems = "stretch";
    wrapper.style.justifyContent = "stretch";
    wrapper.style.padding = "0";
    wrapper.style.flex = isSecondary ? "0 0 200px" : "1 1 auto";
    wrapper.style.border = isSpeaking ? "2px solid #5edc93" : "1px solid rgba(154, 164, 178, 0.16)";
    wrapper.style.borderRadius = "22px";
    wrapper.style.overflow = "hidden";
    wrapper.style.background = "#0f151d";
    wrapper.style.cursor = "pointer";
    wrapper.style.minHeight = isFocused ? "420px" : isSecondary ? "126px" : "240px";
    wrapper.style.boxShadow = isFocused
        ? "0 22px 48px rgba(0, 0, 0, 0.35)"
        : "0 12px 28px rgba(0, 0, 0, 0.18)";
    wrapper.style.aspectRatio = isFocused ? "16 / 8.5" : isSecondary ? "16 / 9" : "16 / 10";
    wrapper.onclick = () => {
        focusedTileKey = focusedTileKey === tile.key ? null : tile.key;
        secondaryTilesCollapsed = false;
        renderVideoGrid();
    };

    const body = document.createElement("div");
    body.style.position = "relative";
    body.style.flex = "1";
    body.style.display = "flex";
    body.style.alignItems = "center";
    body.style.justifyContent = "center";
    body.style.background = tile.type === "screen"
        ? "#06080b"
        : "linear-gradient(160deg, #0f151d 0%, #16212c 100%)";

    if (tile.videoPublication) {
        const element = tile.videoPublication.track.attach();
        element.autoplay = true;
        element.playsInline = true;
        element.muted = tile.videoPublication.isLocal;
        element.style.width = "100%";
        element.style.height = "100%";
        element.style.objectFit = tile.type === "screen" ? "contain" : "cover";
        body.appendChild(element);
        renderedVideoElements.push({ element, track: tile.videoPublication.track });
    }
    else {
        body.appendChild(createPlaceholderAvatar(participant));
    }

    const footer = document.createElement("div");
    footer.style.position = "absolute";
    footer.style.left = "10px";
    footer.style.right = "10px";
    footer.style.bottom = "10px";
    footer.style.display = "flex";
    footer.style.alignItems = "center";
    footer.style.justifyContent = "space-between";
    footer.style.gap = "8px";

    const title = createBadge(tile.label, {
        maxWidth: "70%",
        overflow: "hidden",
        textOverflow: "ellipsis",
        whiteSpace: "nowrap"
    });

    const badges = document.createElement("div");
    badges.style.display = "flex";
    badges.style.gap = "8px";
    badges.style.flexWrap = "wrap";

    if (tile.type === "screen") {
        badges.appendChild(createIconBadge(
            createSvgIcon("M3 5h18c1.1 0 2 .9 2 2v10c0 1.1-.9 2-2 2h-7l2 2v1H8v-1l2-2H3c-1.1 0-2-.9-2-2V7c0-1.1.9-2 2-2Zm0 2v10h18V7H3Z", 18),
            { background: "rgba(47, 189, 99, 0.84)" }
        ));
    }

    if (tile.type !== "screen" && participant.isMuted) {
        badges.appendChild(createIconBadge(
            createSvgIcon("M12 14.56V17c0 1.1-.9 2-2 2s-2-.9-2-2v-1.44l4 4ZM14 9.73V7c0-2.21-1.79-4-4-4-.88 0-1.7.29-2.36.77L14 9.73ZM4.27 3 3 4.27l5 5V13c0 .55.45 1 1 1h2l4 4v-3.73l4.73 4.73L21 17.73 4.27 3Z", 18),
            { background: "rgba(255, 170, 66, 0.82)" }
        ));
    }

    if (tile.type !== "screen" && participant.isDeafened) {
        badges.appendChild(createIconBadge(
            createSvgIcon("M19 11h-1.7c0 .74-.16 1.43-.43 2.05l1.23 1.23A6.955 6.955 0 0 0 19 11Zm-4 .17-3-3V5c0-1.66-1.34-3-3-3S6 3.34 6 5v.18l9 9ZM5.27 3 4 4.27 7.73 8H7c-1.1 0-2 .9-2 2v3c0 .55.45 1 1 1h2l4 4v-3.73L18.73 21 20 19.73 5.27 3Z", 18),
            { background: "rgba(230, 72, 72, 0.84)" }
        ));
    }

    if (isSpeaking) {
        badges.appendChild(createBadge("Speaking", { background: "rgba(47, 189, 99, 0.84)" }));
    }

    footer.appendChild(title);
    footer.appendChild(badges);
    body.appendChild(footer);
    wrapper.appendChild(body);
    return wrapper;
}

function createToggleGlyph(direction) {
    const glyph = document.createElement("span");
    glyph.textContent = direction === "down" ? "▾" : "▴";
    glyph.style.fontSize = "14px";
    glyph.style.lineHeight = "1";
    return glyph;
}

function createPeopleGlyph() {
    const ns = "http://www.w3.org/2000/svg";
    const svg = document.createElementNS(ns, "svg");
    svg.setAttribute("viewBox", "0 0 24 24");
    svg.setAttribute("width", "14");
    svg.setAttribute("height", "14");
    svg.style.display = "block";

    const path = document.createElementNS(ns, "path");
    path.setAttribute(
        "d",
        "M16 11c1.66 0 2.99-1.34 2.99-3S17.66 5 16 5s-3 1.34-3 3 1.34 3 3 3Zm-8 0c1.66 0 2.99-1.34 2.99-3S9.66 5 8 5 5 6.34 5 8s1.34 3 3 3Zm0 2c-2.33 0-7 1.17-7 3.5V19h14v-2.5C15 14.17 10.33 13 8 13Zm8 0c-.29 0-.62.02-.97.05 1.16.84 1.97 1.98 1.97 3.45V19h6v-2.5c0-2.33-4.67-3.5-7-3.5Z"
    );
    path.setAttribute("fill", "currentColor");
    svg.appendChild(path);
    return svg;
}

function createSecondaryToggleButton(collapsed, onClick) {
    const button = document.createElement("button");
    button.type = "button";
    button.style.display = "inline-flex";
    button.style.alignItems = "center";
    button.style.justifyContent = "center";
    button.style.gap = "6px";
    button.style.height = "34px";
    button.style.padding = "0 12px";
    button.style.borderRadius = "999px";
    button.style.border = "1px solid rgba(160, 175, 196, 0.14)";
    button.style.background = "rgba(11, 17, 24, 0.88)";
    button.style.boxShadow = "0 10px 24px rgba(0, 0, 0, 0.28)";
    button.style.color = "#eef3f8";
    button.style.cursor = "pointer";
    button.style.backdropFilter = "blur(10px)";
    button.style.opacity = "0";
    button.style.transition = "opacity 120ms ease";
    button.appendChild(createToggleGlyph(collapsed ? "up" : "down"));
    button.appendChild(createPeopleGlyph());
    button.onclick = event => {
        event.stopPropagation();
        onClick();
    };
    return button;
}

function attachHoverReveal(host, button) {
    host.addEventListener("mouseenter", () => {
        button.style.opacity = "1";
    });

    host.addEventListener("mouseleave", () => {
        button.style.opacity = "0";
    });
}

function renderVideoGrid() {
    const root = getVideoRoot();
    if (!root) {
        return;
    }

    clearRenderedVideoElements();
    root.replaceChildren();

    const tiles = buildVisualTiles();
    if (tiles.length === 0) {
        const empty = document.createElement("div");
        empty.textContent = "Nobody is connected to this call yet.";
        empty.style.padding = "24px";
        empty.style.border = "1px dashed rgba(154, 164, 178, 0.4)";
        empty.style.borderRadius = "20px";
        empty.style.color = "#9aa4b2";
        empty.style.textAlign = "center";
        root.appendChild(empty);
        return;
    }

    if (focusedTileKey && !tiles.some(tile => tile.key === focusedTileKey)) {
        focusedTileKey = null;
        secondaryTilesCollapsed = false;
    }

    if (!focusedTileKey) {
        root.style.display = "grid";
        root.style.gap = "12px";
        root.style.gridTemplateColumns = "repeat(auto-fit, minmax(240px, 1fr))";
        root.style.alignContent = "center";
        root.style.justifyItems = "stretch";

        for (const tile of tiles) {
            root.appendChild(createTileElement(tile, false, false));
        }

        return;
    }

    const focusedTile = tiles.find(tile => tile.key === focusedTileKey);
    const remainingTiles = tiles.filter(tile => tile.key !== focusedTileKey);

    root.style.display = "flex";
    root.style.flexDirection = "column";
    root.style.gap = "12px";

    if (focusedTile) {
        const focusedElement = createTileElement(focusedTile, true, false);
        focusedElement.style.width = "100%";
        focusedElement.style.maxWidth = "100%";

        if (secondaryTilesCollapsed && remainingTiles.length > 0) {
            const expandButton = createSecondaryToggleButton(true, () => {
                secondaryTilesCollapsed = false;
                renderVideoGrid();
            });
            expandButton.style.position = "absolute";
            expandButton.style.left = "50%";
            expandButton.style.bottom = "14px";
            expandButton.style.transform = "translateX(-50%)";
            attachHoverReveal(focusedElement, expandButton);
            focusedElement.appendChild(expandButton);
        }

        root.appendChild(focusedElement);
    }

    if (remainingTiles.length > 0 && !secondaryTilesCollapsed) {
        const strip = document.createElement("div");
        strip.style.display = "flex";
        strip.style.gap = "12px";
        strip.style.justifyContent = "center";
        strip.style.alignItems = "center";
        strip.style.flexWrap = "wrap";
        strip.style.overflow = "hidden";
        strip.style.paddingBottom = "2px";
        strip.style.width = "100%";
        strip.style.position = "relative";
        strip.style.minHeight = "138px";

        const collapseButton = createSecondaryToggleButton(false, () => {
            secondaryTilesCollapsed = true;
            renderVideoGrid();
        });
        collapseButton.style.position = "absolute";
        collapseButton.style.left = "50%";
        collapseButton.style.top = "50%";
        collapseButton.style.transform = "translate(-50%, -50%)";
        attachHoverReveal(strip, collapseButton);

        for (const tile of remainingTiles) {
            strip.appendChild(createTileElement(tile, false, true));
        }

        strip.appendChild(collapseButton);
        root.appendChild(strip);
    }
}

function upsertVideoPublication(track, publication, participant, isLocal = false) {
    if (track.kind !== "video") {
        return;
    }

    const identity = getParticipantIdentity(participant);
    const source = normalizeVideoSource(track, publication);
    if (!identity || !source) {
        return;
    }

    const key = getVideoPublicationKey(identity, source);
    videoPublications.set(key, {
        key,
        identity,
        source,
        track,
        isLocal
    });
    notifyVideoParticipantsChanged();
    renderVideoGrid();
}

function removeVideoPublication(publication, participant, track) {
    const identity = getParticipantIdentity(participant);
    const source = normalizeVideoSource(track, publication);
    if (!identity || !source) {
        return;
    }

    videoPublications.delete(getVideoPublicationKey(identity, source));
    notifyVideoParticipantsChanged();
    renderVideoGrid();
}

function clearVideoPublications() {
    videoPublications.clear();
    focusedTileKey = null;
    secondaryTilesCollapsed = false;
    notifyVideoParticipantsChanged();
}

export async function joinVoiceChannel(serverUrl, token, dotNetObjectReference) {
    if (!window.LivekitClient) {
        throw new Error("LiveKit client SDK is not loaded.");
    }

    await leaveVoiceChannel();
    dotNetRef = dotNetObjectReference;

    room = new window.LivekitClient.Room({
        adaptiveStream: true,
        dynacast: true
    });

    room.on(window.LivekitClient.RoomEvent.TrackSubscribed, (track, publication, participant) => {
        attachRemoteAudioTrack(track, publication, participant);
        upsertVideoPublication(track, publication, participant, false);
    });

    room.on(window.LivekitClient.RoomEvent.TrackUnsubscribed, (track, publication, participant) => {
        detachRemoteAudioTrack(publication, participant);
        removeVideoPublication(publication, participant, track);
    });

    room.on(window.LivekitClient.RoomEvent.ParticipantDisconnected, participant => {
        for (const publication of participant.trackPublications.values()) {
            detachRemoteAudioTrack(publication, participant);
            if (publication.track) {
                removeVideoPublication(publication, participant, publication.track);
            }
        }
    });

    room.on(window.LivekitClient.RoomEvent.ActiveSpeakersChanged, participants => {
        activeSpeakerIds.clear();
        for (const participant of participants) {
            if (participant.identity) {
                activeSpeakerIds.add(participant.identity);
            }
        }

        notifyActiveSpeakersChanged();
        renderVideoGrid();
    });

    await room.connect(serverUrl, token);
    await room.localParticipant.setMicrophoneEnabled(true);

    for (const participant of room.remoteParticipants.values()) {
        for (const publication of participant.trackPublications.values()) {
            if (publication.track) {
                attachRemoteAudioTrack(publication.track, publication, participant);
                upsertVideoPublication(publication.track, publication, participant, false);
            }
        }
    }

    renderVideoGrid();

    if (!room.canPlaybackAudio) {
        try {
            await room.startAudio();
        } catch {
            // Browsers may still require an explicit user interaction later.
        }
    }
}

export async function leaveVoiceChannel() {
    const currentRoom = room;
    room = null;

    if (!currentRoom) {
        detachAllRemoteAudioTracks();
        clearVideoPublications();
        voiceParticipants.clear();
        activeSpeakerIds.clear();
        isDeafened = false;
        renderVideoGrid();
        if (dotNetRef) {
            await dotNetRef.invokeMethodAsync("HandleActiveSpeakersChanged", []);
            await dotNetRef.invokeMethodAsync("HandleVideoParticipantsChanged", []);
        }
        return;
    }

    try {
        await stopScreenShare();
    } catch {
        // Ignore teardown failures during user-initiated leave.
    }

    try {
        await currentRoom.disconnect();
    } catch {
        // LiveKit can surface a client-initiated disconnect as an exception while switching rooms.
    }

    detachAllRemoteAudioTracks();
    clearVideoPublications();
    voiceParticipants.clear();
    activeSpeakerIds.clear();
    isDeafened = false;
    renderVideoGrid();
    if (dotNetRef) {
        await dotNetRef.invokeMethodAsync("HandleActiveSpeakersChanged", []);
        await dotNetRef.invokeMethodAsync("HandleVideoParticipantsChanged", []);
    }
}

export function setVoiceParticipants(participants) {
    voiceParticipants.clear();
    for (const participant of participants) {
        voiceParticipants.set(participant.userId, participant);
    }

    renderVideoGrid();
}

export async function setVoiceMuted(isMuted) {
    if (!room) {
        return;
    }

    await room.localParticipant.setMicrophoneEnabled(!isMuted);
}

export function setVoiceDeafened(deafened) {
    isDeafened = deafened;
    applyPlaybackMute();
}

export async function setCameraEnabled(isEnabled) {
    if (!room) {
        return;
    }

    await room.localParticipant.setCameraEnabled(isEnabled);

    const publication = room.localParticipant.getTrackPublication(window.LivekitClient.Track.Source.Camera);
    if (!isEnabled || !publication?.track) {
        removeVideoPublication(publication, room.localParticipant, publication?.track ?? null);
        return;
    }

    upsertVideoPublication(publication.track, publication, room.localParticipant, true);
}

async function stopScreenShare() {
    if (!room || screenShareTracks.length === 0) {
        screenShareTracks = [];
        return;
    }

    for (const track of screenShareTracks) {
        try {
            await room.localParticipant.unpublishTrack(track, true);
        } catch {
            // Ignore unpublish failures during teardown.
        }

        removeVideoPublication({ source: track.source }, room.localParticipant, track);

        try {
            track.stop();
        } catch {
            // Ignore track stop failures during teardown.
        }
    }

    screenShareTracks = [];
}

export async function setScreenShareEnabled(isEnabled) {
    if (!room) {
        return {
            started: false,
            audioIncluded: false,
            fellBackToVideoOnly: false
        };
    }

    if (!isEnabled) {
        await stopScreenShare();
        return {
            started: false,
            audioIncluded: false,
            fellBackToVideoOnly: false
        };
    }

    await stopScreenShare();
    let tracks;
    let fellBackToVideoOnly = false;
    try {
        tracks = await room.localParticipant.createScreenTracks({
            audio: true
        });
    } catch {
        fellBackToVideoOnly = true;
        tracks = await room.localParticipant.createScreenTracks({
            audio: false
        });
    }

    for (const track of tracks) {
        const publication = await room.localParticipant.publishTrack(track);
        if (track.kind === "video") {
            upsertVideoPublication(track, publication, room.localParticipant, true);
        }

        const mediaStreamTrack = track.mediaStreamTrack;
        if (mediaStreamTrack) {
            mediaStreamTrack.addEventListener("ended", async () => {
                await stopScreenShare();
                renderVideoGrid();
            }, { once: true });
        }
    }

    screenShareTracks = tracks;
    return {
        started: true,
        audioIncluded: tracks.some(track => track.kind === "audio"),
        fellBackToVideoOnly
    };
}

export async function debugScreenCapture() {
    const results = {
        userAgent: navigator.userAgent,
        attempts: []
    };

    for (const audioEnabled of [true, false]) {
        const attempt = {
            audioRequested: audioEnabled
        };

        try {
            const stream = await navigator.mediaDevices.getDisplayMedia({
                video: true,
                audio: audioEnabled
            });

            const tracks = stream.getTracks().map(track => ({
                kind: track.kind,
                label: track.label,
                enabled: track.enabled,
                muted: track.muted,
                readyState: track.readyState,
                settings: typeof track.getSettings === "function" ? track.getSettings() : null
            }));

            attempt.success = true;
            attempt.tracks = tracks;

            for (const track of stream.getTracks()) {
                try {
                    track.stop();
                } catch {
                    // Ignore stop failures in diagnostics.
                }
            }
        } catch (error) {
            attempt.success = false;
            attempt.error = {
                name: error?.name ?? null,
                message: error?.message ?? String(error)
            };
        }

        results.attempts.push(attempt);
    }

    console.log("RelayChat screen capture debug", results);
    return results;
}
