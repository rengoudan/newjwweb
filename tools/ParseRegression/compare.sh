#!/usr/bin/env bash
# 用新版解析器跑与旧版基准相同的“图纸 × 解析设定”组合，并逐行比对。
# 用法: compare.sh <baseline 目录（含 matrix.log 与 fN_sM.txt）> <输出目录>
set -u
B="$1"; O="$2"; mkdir -p "$O"
R="$(dirname "$0")/bin/Debug/net10.0/ParseRegression.exe"
FILES=("D:/code/jw/1伏図.jww" "D:/code/jw/2伏図.jww" "D:/code/jw/2FC.jww" "D:/code/jwweb/モレスヴィラ荒戎(岡林氏仕様).jww" "D:/code/jw/積算根拠(伏図20240722).jww")
declare -A SET
SET[1]="3 10 10 9 1 0.0 3 3 0 0 0 0 0"; SET[2]="5 10 3 6 1 5.0 5 3 2 0 1 1 0"; SET[3]="5 8 3 -1 -1 5.0 3 4 2 0 1 0 0"
SET[4]="3 10 10 -1 -1 5.0 3 10 8 1 0 0 0"; SET[5]="2 -1 3 -1 -1 0.0 2 4 5 1 0 0 0"; SET[6]="3 2 6 -1 -1 5.0 3 4 8 1 0 0 0"
SET[7]="3 6 8 -1 -1 5.0 3 -1 1 1 0 0 0"
grep -E '^f[0-9]+ s[0-9]+ OK' "$B/matrix.log" | while read -r f s rest; do
  fi=${f#f}; si=${s#s}
  "$R" run "${FILES[$((fi-1))]}" "$O/${f}_${s}.txt" ${SET[$si]} > "$O/${f}_${s}.run" 2>&1
  printf '%s %s old[%s] new[%s] => %s\n' "$f" "$s" "$(echo "$rest" | cut -c1-40)" "$(cut -c1-40 "$O/${f}_${s}.run")" "$("$R" diff "$B/${f}_${s}.txt" "$O/${f}_${s}.txt" | head -1)"
done
