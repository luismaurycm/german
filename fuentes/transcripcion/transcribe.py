import sys, time
from faster_whisper import WhisperModel

model_name, out_dir, *files = sys.argv[1:]
model = WhisperModel(model_name, device="cpu", compute_type="int8", cpu_threads=8)
for f in files:
    t0 = time.time()
    segs, info = model.transcribe(f, language="es", vad_filter=True, beam_size=5)
    name = f.replace("\\", "/").split("/")[-1].rsplit(".", 1)[0]
    with open(f"{out_dir}/{name}.txt", "w", encoding="utf-8") as fh:
        fh.write(f"# {name} (duracion {info.duration/60:.1f} min)\n\n")
        for s in segs:
            m, sec = divmod(int(s.start), 60)
            fh.write(f"[{m:02d}:{sec:02d}] {s.text.strip()}\n")
            fh.flush()
    print(f"{name}: {info.duration/60:.1f} min audio, {time.time()-t0:.0f}s", flush=True)
