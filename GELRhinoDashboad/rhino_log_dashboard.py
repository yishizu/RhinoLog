# rhino_log_dashboard.py
import streamlit as st
import pandas as pd
import json
import os
import plotly.express as px

st.set_page_config(page_title="Rhino Training Log Dashboard", layout="wide")
st.title("📊 Rhino Training Log Dashboard")

# --- ファイルアップロード ---
log_file = st.file_uploader("Upload Log CSV", type="csv")
meta_file = st.file_uploader("Upload Meta JSON", type="json")

if log_file and meta_file:
    # --- CSV 読み込み ---
    log_df = pd.read_csv(log_file)
    log_df['Timestamp'] = pd.to_datetime(log_df['Timestamp'])

    # --- JSON 読み込み ---
    meta = json.load(meta_file)

    st.subheader("📁 Meta Information")
    st.json(meta, expanded=False)

    st.subheader("🧠 Operation Log")
    st.dataframe(log_df.tail(20), use_container_width=True)

    # --- イベントの頻度 ---
    freq = log_df['Action'].value_counts().reset_index()
    freq.columns = ['Action', 'Count']
    fig_bar = px.bar(freq, x='Action', y='Count', title="Event Frequency", text='Count')
    st.plotly_chart(fig_bar, use_container_width=True)

    # --- 簡易スコアリング（例） ---
    score = {
        "Modeling": freq.query("Action == 'Object Added'")['Count'].sum(),
        "Cleaning": freq.query("Action == 'Object Deleted'")['Count'].sum(),
        "Command": freq.query("Action == 'Command Started'")['Count'].sum(),
        "Saving": freq.query("Action == 'File Saved'")['Count'].sum(),
        "ViewOps": freq.query("Action.str.contains('View')", engine='python')['Count'].sum()
    }
    radar_df = pd.DataFrame({"Skill": list(score.keys()), "Score": list(score.values())})
    fig_radar = px.line_polar(radar_df, r='Score', theta='Skill', line_close=True, title="Skill Radar")
    st.plotly_chart(fig_radar, use_container_width=True)

else:
    st.info("Please upload both a log CSV and a meta JSON file to begin.")
